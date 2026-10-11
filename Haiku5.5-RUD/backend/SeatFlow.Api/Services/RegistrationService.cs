using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;

namespace SeatFlow.Api.Services;

public sealed record RegistrationResult(Registration Registration, bool Replayed);

public sealed record CancellationResult(Registration Registration, bool Changed, Registration? Promoted);

/// <summary>
/// Owns the registration rules: duplicate prevention, capacity and waitlist,
/// schedule conflicts, FIFO promotion, idempotency and cancellation.
/// </summary>
public sealed class RegistrationService(
    IDbContextFactory<SeatFlowDbContext> dbFactory,
    INotificationService notifications,
    TimeProvider clock,
    ILogger<RegistrationService> logger)
{
    // SQLite allows one writer at a time, and the count-then-insert capacity check is not atomic on its own.
    // This process-wide lock serialises every registration and cancellation, so two requests can never both
    // take the last seat. The database also enforces one active registration per participant and workshop,
    // and unique idempotency keys, as a backstop. The lock only covers one process; see README limitations.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<RegistrationResult> RegisterAsync(
        int workshopId, int participantId, string? idempotencyKey, CancellationToken ct = default)
    {
        RegistrationResult result;

        await _writeLock.WaitAsync(ct);
        try
        {
            result = await RegisterCoreAsync(workshopId, participantId, idempotencyKey, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        // Notifications are sent after commit and never roll back the registration.
        if (!result.Replayed && result.Registration.Status == RegistrationStatus.Confirmed)
        {
            var reg = result.Registration;
            await NotifySafelyAsync(new Notification(
                NotificationKind.RegistrationConfirmed, reg.ParticipantId, reg.WorkshopId, reg.Id,
                $"Your registration for '{reg.Workshop.Title}' is confirmed."), ct);
        }

        return result;
    }

    public async Task<CancellationResult> CancelAsync(int registrationId, CancellationToken ct = default)
    {
        CancellationResult result;

        await _writeLock.WaitAsync(ct);
        try
        {
            result = await CancelCoreAsync(registrationId, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        if (!result.Changed)
        {
            return result;
        }

        var reg = result.Registration;
        await NotifySafelyAsync(new Notification(
            NotificationKind.RegistrationCancelled, reg.ParticipantId, reg.WorkshopId, reg.Id,
            $"Your registration for '{reg.Workshop.Title}' was cancelled."), ct);

        if (result.Promoted is { } promoted)
        {
            await NotifySafelyAsync(new Notification(
                NotificationKind.WaitlistPromoted, promoted.ParticipantId, promoted.WorkshopId, promoted.Id,
                $"You have been promoted from the waitlist for '{promoted.Workshop.Title}'."), ct);
        }

        return result;
    }

    private async Task<RegistrationResult> RegisterCoreAsync(
        int workshopId, int participantId, string? idempotencyKey, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Idempotency: a repeated key returns the registration created by the first request.
        if (idempotencyKey is not null)
        {
            var existing = await db.Registrations
                .Include(r => r.Workshop)
                .Include(r => r.Participant)
                .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);

            if (existing is not null)
            {
                if (existing.WorkshopId != workshopId || existing.ParticipantId != participantId)
                {
                    throw new SeatFlowException(SeatFlowErrorKind.Conflict,
                        "This Idempotency-Key was already used for a different registration request.");
                }

                return new RegistrationResult(existing, Replayed: true);
            }
        }

        var workshop = await db.Workshops.FindAsync([workshopId], ct)
            ?? throw new SeatFlowException(SeatFlowErrorKind.NotFound, $"Workshop {workshopId} was not found.");
        var participant = await db.Participants.FindAsync([participantId], ct)
            ?? throw new SeatFlowException(SeatFlowErrorKind.NotFound, $"Participant {participantId} was not found.");

        var alreadyActive = await db.Registrations.AnyAsync(r =>
            r.WorkshopId == workshopId && r.ParticipantId == participantId && r.Status != RegistrationStatus.Cancelled, ct);
        if (alreadyActive)
        {
            throw new SeatFlowException(SeatFlowErrorKind.Conflict,
                "Participant already has an active registration for this workshop.");
        }

        if (await HasScheduleConflictAsync(db, participantId, workshop, ct))
        {
            throw new SeatFlowException(SeatFlowErrorKind.Conflict,
                "Participant already has a confirmed workshop that overlaps this one.");
        }

        var confirmedCount = await CountConfirmedAsync(db, workshopId, ct);
        var registration = new Registration
        {
            WorkshopId = workshop.Id,
            Workshop = workshop,
            ParticipantId = participant.Id,
            Participant = participant,
            Status = confirmedCount < workshop.Capacity ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted,
            CreatedAt = clock.GetUtcNow(),
            IdempotencyKey = idempotencyKey
        };

        db.Registrations.Add(registration);
        await db.SaveChangesAsync(ct);

        return new RegistrationResult(registration, Replayed: false);
    }

    private async Task<CancellationResult> CancelCoreAsync(int registrationId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var registration = await db.Registrations
            .Include(r => r.Workshop)
            .Include(r => r.Participant)
            .FirstOrDefaultAsync(r => r.Id == registrationId, ct)
            ?? throw new SeatFlowException(SeatFlowErrorKind.NotFound, $"Registration {registrationId} was not found.");

        // Cancelling an already cancelled registration is a no-op.
        if (registration.Status == RegistrationStatus.Cancelled)
        {
            return new CancellationResult(registration, Changed: false, Promoted: null);
        }

        var freedSeat = registration.Status == RegistrationStatus.Confirmed;
        registration.Status = RegistrationStatus.Cancelled;
        await db.SaveChangesAsync(ct);

        var promoted = freedSeat ? await PromoteNextEligibleAsync(db, registration.Workshop, ct) : null;

        return new CancellationResult(registration, Changed: true, Promoted: promoted);
    }

    /// <summary>
    /// Promotes the oldest waitlisted registration whose participant has no schedule conflict.
    /// Ineligible entries stay on the waitlist in their original position.
    /// </summary>
    private async Task<Registration?> PromoteNextEligibleAsync(SeatFlowDbContext db, Workshop workshop, CancellationToken ct)
    {
        if (await CountConfirmedAsync(db, workshop.Id, ct) >= workshop.Capacity)
        {
            return null;
        }

        // SQLite cannot ORDER BY DateTimeOffset, so the FIFO sort happens in memory (the waitlist is small).
        var waitlist = (await db.Registrations
                .Include(r => r.Workshop)
                .Include(r => r.Participant)
                .Where(r => r.WorkshopId == workshop.Id && r.Status == RegistrationStatus.Waitlisted)
                .ToListAsync(ct))
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id);

        foreach (var candidate in waitlist)
        {
            if (await HasScheduleConflictAsync(db, candidate.ParticipantId, workshop, ct))
            {
                logger.LogInformation("Skipping waitlisted registration {RegistrationId}: schedule conflict.", candidate.Id);
                continue;
            }

            candidate.Status = RegistrationStatus.Confirmed;
            await db.SaveChangesAsync(ct);
            return candidate;
        }

        return null;
    }

    private static Task<int> CountConfirmedAsync(SeatFlowDbContext db, int workshopId, CancellationToken ct) =>
        db.Registrations.CountAsync(r => r.WorkshopId == workshopId && r.Status == RegistrationStatus.Confirmed, ct);

    private static async Task<bool> HasScheduleConflictAsync(
        SeatFlowDbContext db, int participantId, Workshop workshop, CancellationToken ct)
    {
        var confirmedWorkshops = await db.Registrations
            .Where(r => r.ParticipantId == participantId && r.Status == RegistrationStatus.Confirmed)
            .Select(r => r.Workshop)
            .ToListAsync(ct);

        return confirmedWorkshops.Any(w => w.Id != workshop.Id && w.OverlapsWith(workshop));
    }

    private async Task NotifySafelyAsync(Notification notification, CancellationToken ct)
    {
        try
        {
            await notifications.NotifyAsync(notification, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to send {Kind} notification for registration {RegistrationId}.",
                notification.Kind, notification.RegistrationId);
        }
    }
}
