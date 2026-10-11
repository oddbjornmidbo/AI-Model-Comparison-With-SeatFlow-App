using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Services;

public sealed class RegistrationService(SeatFlowDbContext db, INotificationService notifications,
    ILogger<RegistrationService> logger)
{
    public Task<RegistrationDto> RegisterAsync(int workshopId, int participantId, string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (idempotencyKey is not null && (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 200 || idempotencyKey.Any(char.IsControl)))
            throw new ApiException(400, "InvalidIdempotencyKey", "Idempotency-Key must contain 1–200 printable characters.");

        return WriteAsync(async events =>
        {
            if (idempotencyKey is not null)
            {
                var prior = await db.IdempotencyRecords
                    .Include(i => i.Registration).ThenInclude(r => r.Participant)
                    .SingleOrDefaultAsync(i => i.Key == idempotencyKey, cancellationToken);
                if (prior is not null)
                {
                    if (prior.WorkshopId != workshopId || prior.ParticipantId != participantId)
                        throw new ApiException(409, "IdempotencyKeyConflict", "This key was used for a different registration request.");
                    return RegistrationDto.From(prior.Registration);
                }
            }

            var workshop = await db.Workshops.FindAsync([workshopId], cancellationToken)
                ?? throw new ApiException(404, "WorkshopNotFound", "Workshop was not found.");
            var participant = await db.Participants.FindAsync([participantId], cancellationToken)
                ?? throw new ApiException(404, "ParticipantNotFound", "Participant was not found.");
            if (await db.Registrations.AnyAsync(r => r.WorkshopId == workshopId
                && r.ParticipantId == participantId && r.Status != RegistrationStatus.Cancelled, cancellationToken))
                throw new ApiException(409, "DuplicateRegistration", "Participant already has an active registration for this workshop.");

            var confirmedCount = await db.Registrations.CountAsync(r => r.WorkshopId == workshopId
                && r.Status == RegistrationStatus.Confirmed, cancellationToken);
            var status = confirmedCount < workshop.Capacity
                ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted;
            if (status == RegistrationStatus.Confirmed && await HasConflictAsync(participantId, workshop, cancellationToken))
                throw new ApiException(409, "ScheduleConflict", "Participant already has a confirmed workshop overlapping this time.");

            var registration = new Registration
            {
                Workshop = workshop, Participant = participant, Status = status, CreatedAt = DateTime.UtcNow
            };
            db.Registrations.Add(registration);
            if (idempotencyKey is not null)
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = idempotencyKey, WorkshopId = workshopId, ParticipantId = participantId,
                    Registration = registration
                });
            await db.SaveChangesAsync(cancellationToken);
            if (status == RegistrationStatus.Confirmed)
                events.Add(Notification(registration, NotificationKind.Confirmed));
            return RegistrationDto.From(registration);
        }, cancellationToken);
    }

    public Task<RegistrationDto> CancelAsync(int registrationId, CancellationToken cancellationToken = default) =>
        WriteAsync(async events =>
        {
            var registration = await db.Registrations.Include(r => r.Workshop).Include(r => r.Participant)
                .SingleOrDefaultAsync(r => r.Id == registrationId, cancellationToken)
                ?? throw new ApiException(404, "RegistrationNotFound", "Registration was not found.");
            if (registration.Status == RegistrationStatus.Cancelled)
                return RegistrationDto.From(registration);

            var freesSeat = registration.Status == RegistrationStatus.Confirmed;
            registration.Status = RegistrationStatus.Cancelled;
            events.Add(Notification(registration, NotificationKind.Cancelled));
            // Persist the cancellation inside this transaction before querying conflicts.
            await db.SaveChangesAsync(cancellationToken);
            if (freesSeat)
            {
                var waitlist = await db.Registrations.Include(r => r.Participant)
                    .Where(r => r.WorkshopId == registration.WorkshopId && r.Status == RegistrationStatus.Waitlisted)
                    .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToListAsync(cancellationToken);
                foreach (var candidate in waitlist)
                {
                    if (await HasConflictAsync(candidate.ParticipantId, registration.Workshop, cancellationToken))
                        continue;
                    candidate.Status = RegistrationStatus.Confirmed;
                    events.Add(Notification(candidate, NotificationKind.Promoted));
                    await db.SaveChangesAsync(cancellationToken);
                    break;
                }
            }
            return RegistrationDto.From(registration);
        }, cancellationToken);

    private Task<bool> HasConflictAsync(int participantId, Workshop workshop, CancellationToken cancellationToken) =>
        db.Registrations.AnyAsync(r => r.ParticipantId == participantId
            && r.Status == RegistrationStatus.Confirmed
            && r.Workshop.StartTime < workshop.EndTime && workshop.StartTime < r.Workshop.EndTime,
            cancellationToken);

    private static RegistrationNotification Notification(Registration registration, NotificationKind kind) =>
        new(registration.Id, registration.WorkshopId, registration.ParticipantId, kind, DateTime.UtcNow);

    private async Task<RegistrationDto> WriteAsync(Func<List<RegistrationNotification>, Task<RegistrationDto>> action,
        CancellationToken cancellationToken)
    {
        var events = new List<RegistrationNotification>();
        RegistrationDto result;
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        // BEGIN IMMEDIATE obtains SQLite's writer lock BEFORE any capacity/conflict reads.
        // This also protects requests from other processes using the same database file.
        using (var transaction = connection.BeginTransaction(deferred: false))
        {
            await using var enlisted = await db.Database.UseTransactionAsync(transaction, cancellationToken);
            result = await action(events);
            await transaction.CommitAsync(cancellationToken);
        }

        // External effects run only after commit and outside the database lock.
        foreach (var notification in events)
        {
            try
            {
                await notifications.SendAsync(notification, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Notification failed after committing registration {RegistrationId}",
                    notification.RegistrationId);
            }
        }
        return result;
    }
}
