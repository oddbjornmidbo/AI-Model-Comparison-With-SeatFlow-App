using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SeatFlow.Core;

public sealed record WorkshopSummary(Workshop Workshop, int ConfirmedCount, int WaitlistedCount)
{
    public int RemainingCapacity => Math.Max(0, Workshop.Capacity - ConfirmedCount);
}

/// <param name="Registration">The registration that was created or replayed.</param>
/// <param name="Created">False when an earlier request with the same Idempotency-Key was returned.</param>
public sealed record RegistrationResult(Registration Registration, bool Created);

/// <summary>
/// Holds the workshop registration rules. Controllers/endpoints only translate HTTP to these calls.
/// </summary>
public sealed class SeatFlowService(
    IDbContextFactory<SeatFlowDbContext> dbFactory,
    INotificationService notifications,
    ILogger<SeatFlowService> logger)
{
    // SQLite allows a single writer, and capacity and schedule checks are read-then-write.
    // All registration changes are therefore serialised through this lock, which is process-wide.
    // This is sufficient for a single API instance; see README for the multi-instance limitation.
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public async Task<IReadOnlyList<WorkshopSummary>> GetWorkshopsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var workshops = await db.Workshops.OrderBy(w => w.StartTime).ThenBy(w => w.Id).ToListAsync(ct);
        var counts = await db.Registrations
            .Where(r => r.Status != RegistrationStatus.Cancelled)
            .GroupBy(r => new { r.WorkshopId, r.Status })
            .Select(g => new { g.Key.WorkshopId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        return workshops.Select(w => new WorkshopSummary(
            w,
            counts.FirstOrDefault(c => c.WorkshopId == w.Id && c.Status == RegistrationStatus.Confirmed)?.Count ?? 0,
            counts.FirstOrDefault(c => c.WorkshopId == w.Id && c.Status == RegistrationStatus.Waitlisted)?.Count ?? 0))
            .ToList();
    }

    public async Task<Workshop> CreateWorkshopAsync(string? title, DateTime startTime, DateTime endTime, int capacity, CancellationToken ct = default)
    {
        var trimmedTitle = title?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (trimmedTitle.Length == 0) errors["title"] = ["Title is required."];
        else if (trimmedTitle.Length > 200) errors["title"] = ["Title must be 200 characters or fewer."];
        if (endTime <= startTime) errors["endTime"] = ["End time must be after start time."];
        if (capacity < 1) errors["capacity"] = ["Capacity must be at least 1."];
        if (errors.Count > 0) throw new ValidationFailedException(errors);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var workshop = new Workshop
        {
            Title = trimmedTitle,
            StartTime = startTime,
            EndTime = endTime,
            Capacity = capacity
        };
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync(ct);
        return workshop;
    }

    public async Task<IReadOnlyList<Participant>> GetParticipantsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Participants.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Registration>> GetWorkshopRegistrationsAsync(int workshopId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await EnsureWorkshopExistsAsync(db, workshopId, ct);

        var registrations = await db.Registrations
            .Include(r => r.Workshop)
            .Include(r => r.Participant)
            .Where(r => r.WorkshopId == workshopId)
            .ToListAsync(ct);

        // Confirmed first, then waitlist (FIFO), then cancelled.
        return registrations
            .OrderBy(r => r.Status)
            .ThenBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToList();
    }

    /// <summary>
    /// Registers a participant for a workshop. Confirms when there is capacity and no schedule conflict,
    /// otherwise waitlists. Repeating a request with the same Idempotency-Key returns the original registration.
    /// </summary>
    public async Task<RegistrationResult> RegisterAsync(int workshopId, int participantId, string? idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) idempotencyKey = null;

        RegistrationResult result;
        var pending = new List<Notification>();

        await WriteLock.WaitAsync(ct);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            if (idempotencyKey is not null)
            {
                var existing = await db.Registrations
                    .Include(r => r.Workshop)
                    .Include(r => r.Participant)
                    .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);

                if (existing is not null)
                {
                    if (existing.WorkshopId != workshopId || existing.ParticipantId != participantId)
                        throw new ConflictException("This Idempotency-Key was already used for a different registration request.");
                    return new RegistrationResult(existing, Created: false);
                }
            }

            var workshop = await db.Workshops.FindAsync([workshopId], ct)
                ?? throw new NotFoundException($"Workshop {workshopId} was not found.");
            var participant = await db.Participants.FindAsync([participantId], ct)
                ?? throw new NotFoundException($"Participant {participantId} was not found.");

            var alreadyActive = await db.Registrations.AnyAsync(r =>
                r.WorkshopId == workshopId && r.ParticipantId == participantId && r.Status != RegistrationStatus.Cancelled, ct);
            if (alreadyActive)
                throw new ConflictException("Participant is already registered for this workshop.");

            var confirmedCount = await CountConfirmedAsync(db, workshopId, ct);
            RegistrationStatus status;
            if (confirmedCount < workshop.Capacity)
            {
                if (await HasConfirmedScheduleConflictAsync(db, participantId, workshop, ct))
                    throw new ConflictException("Participant already has a confirmed workshop that overlaps this one.");
                status = RegistrationStatus.Confirmed;
            }
            else
            {
                status = RegistrationStatus.Waitlisted;
            }

            var registration = new Registration
            {
                WorkshopId = workshopId,
                ParticipantId = participantId,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                IdempotencyKey = idempotencyKey,
                Workshop = workshop,
                Participant = participant
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync(ct);

            if (status == RegistrationStatus.Confirmed)
                pending.Add(NewNotification(NotificationKind.RegistrationConfirmed, registration,
                    $"You are confirmed for '{workshop.Title}'."));
            else
                logger.LogInformation("Participant {ParticipantId} waitlisted for workshop {WorkshopId}", participantId, workshopId);

            result = new RegistrationResult(registration, Created: true);
        }
        finally
        {
            WriteLock.Release();
        }

        await DispatchAsync(pending);
        return result;
    }

    /// <summary>
    /// Cancels a registration. Cancelling an already cancelled registration is a no-op that returns it.
    /// If a confirmed seat was freed, the oldest eligible waitlisted participant is promoted.
    /// </summary>
    public async Task<Registration> CancelAsync(int registrationId, CancellationToken ct = default)
    {
        var pending = new List<Notification>();
        Registration registration;

        await WriteLock.WaitAsync(ct);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            registration = await db.Registrations
                .Include(r => r.Workshop)
                .Include(r => r.Participant)
                .FirstOrDefaultAsync(r => r.Id == registrationId, ct)
                ?? throw new NotFoundException($"Registration {registrationId} was not found.");

            if (registration.Status == RegistrationStatus.Cancelled)
                return registration;

            var wasConfirmed = registration.Status == RegistrationStatus.Confirmed;
            registration.Status = RegistrationStatus.Cancelled;
            pending.Add(NewNotification(NotificationKind.RegistrationCancelled, registration,
                $"Your registration for '{registration.Workshop.Title}' was cancelled."));
            await db.SaveChangesAsync(ct);

            if (wasConfirmed)
            {
                var promoted = await PromoteNextEligibleAsync(db, registration.Workshop, ct);
                if (promoted is not null)
                {
                    await db.SaveChangesAsync(ct);
                    pending.Add(NewNotification(NotificationKind.WaitlistPromoted, promoted,
                        $"You have been promoted from the waitlist for '{registration.Workshop.Title}'."));
                }
            }
        }
        finally
        {
            WriteLock.Release();
        }

        await DispatchAsync(pending);
        return registration;
    }

    /// <summary>Active (confirmed or waitlisted) registrations for a participant, ordered by workshop start.</summary>
    public async Task<IReadOnlyList<Registration>> GetParticipantScheduleAsync(int participantId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Participants.AnyAsync(p => p.Id == participantId, ct))
            throw new NotFoundException($"Participant {participantId} was not found.");

        var registrations = await db.Registrations
            .Include(r => r.Workshop)
            .Include(r => r.Participant)
            .Where(r => r.ParticipantId == participantId && r.Status != RegistrationStatus.Cancelled)
            .ToListAsync(ct);

        return registrations.OrderBy(r => r.Workshop.StartTime).ThenBy(r => r.Id).ToList();
    }

    /// <summary>
    /// Promotes the oldest waitlisted registration that has no schedule conflict.
    /// Ineligible registrations stay on the waitlist in their original position.
    /// </summary>
    private static async Task<Registration?> PromoteNextEligibleAsync(SeatFlowDbContext db, Workshop workshop, CancellationToken ct)
    {
        if (await CountConfirmedAsync(db, workshop.Id, ct) >= workshop.Capacity)
            return null;

        var waitlist = await db.Registrations
            .Where(r => r.WorkshopId == workshop.Id && r.Status == RegistrationStatus.Waitlisted)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToListAsync(ct);

        foreach (var candidate in waitlist)
        {
            if (await HasConfirmedScheduleConflictAsync(db, candidate.ParticipantId, workshop, ct))
                continue;

            candidate.Status = RegistrationStatus.Confirmed;
            return candidate;
        }

        return null;
    }

    private static Task<int> CountConfirmedAsync(SeatFlowDbContext db, int workshopId, CancellationToken ct) =>
        db.Registrations.CountAsync(r => r.WorkshopId == workshopId && r.Status == RegistrationStatus.Confirmed, ct);

    private static async Task<bool> HasConfirmedScheduleConflictAsync(SeatFlowDbContext db, int participantId, Workshop workshop, CancellationToken ct)
    {
        var confirmedWorkshops = await db.Registrations
            .Where(r => r.ParticipantId == participantId && r.Status == RegistrationStatus.Confirmed && r.WorkshopId != workshop.Id)
            .Select(r => r.Workshop)
            .ToListAsync(ct);

        return confirmedWorkshops.Any(workshop.OverlapsWith);
    }

    private static async Task EnsureWorkshopExistsAsync(SeatFlowDbContext db, int workshopId, CancellationToken ct)
    {
        if (!await db.Workshops.AnyAsync(w => w.Id == workshopId, ct))
            throw new NotFoundException($"Workshop {workshopId} was not found.");
    }

    private static Notification NewNotification(NotificationKind kind, Registration registration, string message) =>
        new(kind, registration.Id, registration.ParticipantId, registration.WorkshopId, message, DateTime.UtcNow);

    /// <summary>
    /// Sends notifications after the state change has been committed. Failures are logged and never
    /// roll back or fail the registration or cancellation.
    /// </summary>
    private async Task DispatchAsync(IEnumerable<Notification> pending)
    {
        foreach (var notification in pending)
        {
            try
            {
                await notifications.NotifyAsync(notification);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send {Kind} notification for registration {RegistrationId}",
                    notification.Kind, notification.RegistrationId);
            }
        }
    }
}
