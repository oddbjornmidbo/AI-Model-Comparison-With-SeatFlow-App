using Microsoft.EntityFrameworkCore;

namespace SeatFlow.Api;

public interface INotificationService
{
    Task NotifyAsync(string message, CancellationToken cancellationToken = default);
}

public sealed class MemoryNotificationService(ILogger<MemoryNotificationService> logger) : INotificationService
{
    private readonly List<string> _messages = [];
    public IReadOnlyList<string> Messages => _messages;
    public Task NotifyAsync(string message, CancellationToken cancellationToken = default)
    {
        lock (_messages) _messages.Add(message);
        logger.LogInformation("SeatFlow notification: {Message}", message);
        return Task.CompletedTask;
    }
}

public sealed class SeatFlowException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class RegistrationService(IDbContextFactory<SeatFlowDbContext> contextFactory, INotificationService notifications)
{
    // SQLite serializes writes. This gate also prevents competing read-then-write decisions in this process.
    private static readonly SemaphoreSlim MutationGate = new(1, 1);

    public async Task<(Registration Registration, bool Created)> RegisterAsync(int workshopId, int participantId, string? idempotencyKey, CancellationToken ct)
    {
        await MutationGate.WaitAsync(ct);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var workshop = await db.Workshops.FindAsync([workshopId], ct) ?? throw new SeatFlowException("Workshop not found.", 404);
            var participant = await db.Participants.FindAsync([participantId], ct) ?? throw new SeatFlowException("Participant not found.", 404);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var previousKey = await db.IdempotencyRecords.SingleOrDefaultAsync(x => x.Key == idempotencyKey, ct);
                if (previousKey is not null)
                {
                    if (previousKey.WorkshopId != workshopId || previousKey.ParticipantId != participantId)
                        throw new SeatFlowException("Idempotency-Key was already used for a different request.", 409);
                    var oldRegistration = await db.Registrations.FindAsync([previousKey.RegistrationId], ct);
                    if (oldRegistration is not null) return (oldRegistration, false);
                }
            }

            var registration = await db.Registrations.SingleOrDefaultAsync(x => x.WorkshopId == workshopId && x.ParticipantId == participantId, ct);
            if (registration is not null && registration.Status != RegistrationStatus.Cancelled)
                throw new SeatFlowException("Participant already has a registration for this workshop.", 409);

            var confirmed = await db.Registrations.CountAsync(x => x.WorkshopId == workshopId && x.Status == RegistrationStatus.Confirmed, ct);
            var hasConflict = await HasConflictAsync(db, participantId, workshop, ct);
            if (hasConflict && confirmed < workshop.Capacity)
                throw new SeatFlowException("Participant has a confirmed workshop at an overlapping time.", 409);

            if (registration is null)
            {
                registration = new Registration { WorkshopId = workshopId, ParticipantId = participantId, CreatedAt = DateTimeOffset.UtcNow };
                db.Registrations.Add(registration);
            }
            registration.CreatedAt = DateTimeOffset.UtcNow;
            registration.Status = confirmed < workshop.Capacity && !hasConflict ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted;
            await db.SaveChangesAsync(ct);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
                db.IdempotencyRecords.Add(new IdempotencyRecord { Key = idempotencyKey, WorkshopId = workshopId, ParticipantId = participantId, RegistrationId = registration.Id });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await NotifySafelyAsync($"Registration {registration.Id} became {registration.Status}.", ct);
            return (registration, true);
        }
        finally { MutationGate.Release(); }
    }

    public async Task<Registration> CancelAsync(int registrationId, CancellationToken ct)
    {
        await MutationGate.WaitAsync(ct);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var registration = await db.Registrations.Include(x => x.Workshop).Include(x => x.Participant)
                .SingleOrDefaultAsync(x => x.Id == registrationId, ct) ?? throw new SeatFlowException("Registration not found.", 404);
            if (registration.Status == RegistrationStatus.Cancelled) return registration;
            var wasConfirmed = registration.Status == RegistrationStatus.Confirmed;
            Registration? promoted = null;
            registration.Status = RegistrationStatus.Cancelled;
            await db.SaveChangesAsync(ct);
            if (wasConfirmed)
            {
                var queued = await db.Registrations.Include(x => x.Participant)
                    .Where(x => x.WorkshopId == registration.WorkshopId && x.Status == RegistrationStatus.Waitlisted)
                    .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
                foreach (var candidate in queued)
                {
                    if (await HasConflictAsync(db, candidate.ParticipantId, registration.Workshop!, ct)) continue;
                    candidate.Status = RegistrationStatus.Confirmed;
                    await db.SaveChangesAsync(ct);
                    promoted = candidate;
                    break;
                }
            }
            await tx.CommitAsync(ct);
            if (promoted is not null) await NotifySafelyAsync($"Waitlisted registration {promoted.Id} was promoted.", ct);
            await NotifySafelyAsync($"Registration {registration.Id} was cancelled.", ct);
            return registration;
        }
        finally { MutationGate.Release(); }
    }

    private static async Task<bool> HasConflictAsync(SeatFlowDbContext db, int participantId, Workshop target, CancellationToken ct)
    {
        var intervals = await db.Registrations.Where(r => r.ParticipantId == participantId &&
                r.Status == RegistrationStatus.Confirmed && r.WorkshopId != target.Id)
            .Select(r => new { r.Workshop!.StartsAt, r.Workshop.EndsAt }).ToListAsync(ct);
        return intervals.Any(x => x.StartsAt < target.EndsAt && target.StartsAt < x.EndsAt);
    }

    private async Task NotifySafelyAsync(string message, CancellationToken ct)
    {
        try { await notifications.NotifyAsync(message, ct); }
        catch (Exception ex) { /* External notification failures must never undo committed business changes. */
            // Logging intentionally omitted here: service can be used without an ASP.NET logger.
            _ = ex;
        }
    }
}
