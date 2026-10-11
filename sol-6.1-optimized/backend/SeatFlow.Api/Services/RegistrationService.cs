using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Services;

public sealed class RegistrationService(SeatFlowDbContext db, INotificationService notifications,
    TimeProvider clock, ILogger<RegistrationService> logger)
{
    public async Task<RegistrationResult> RegisterAsync(long workshopId, long participantId, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200 || idempotencyKey.Any(char.IsControl))
            throw new DomainException(400, "invalid_idempotency_key", "Supply an Idempotency-Key header between 1 and 200 characters.");
        if (participantId <= 0)
            throw new DomainException(400, "invalid_participant", "Select an existing participant.");

        var pendingNotifications = new List<RegistrationNotification>();
        var result = await InWriteTransactionAsync(async () =>
        {
            var fingerprint = $"register:{workshopId}:{participantId}";
            var previous = await db.IdempotencyRequests.AsNoTracking()
                .SingleOrDefaultAsync(r => r.Key == idempotencyKey, cancellationToken);
            if (previous is not null)
            {
                if (previous.Fingerprint != fingerprint)
                    throw new DomainException(409, "idempotency_key_conflict", "This idempotency key was already used for a different registration request.");
                var original = JsonSerializer.Deserialize<RegistrationDto>(previous.ResponseJson, ApiJson.Options)
                    ?? throw new InvalidOperationException("The persisted idempotency response is invalid.");
                return new RegistrationResult(original, true);
            }

            var workshop = await db.Workshops.SingleOrDefaultAsync(w => w.Id == workshopId, cancellationToken)
                ?? throw new DomainException(404, "workshop_not_found", "This workshop could not be found.");
            if (!await db.Participants.AnyAsync(p => p.Id == participantId, cancellationToken))
                throw new DomainException(404, "participant_not_found", "This participant could not be found.");
            if (await db.Registrations.AnyAsync(r => r.WorkshopId == workshopId && r.ParticipantId == participantId
                    && r.Status != RegistrationStatus.Cancelled, cancellationToken))
                throw new DomainException(409, "duplicate_registration", "This participant already has an active registration for this workshop.");

            var confirmed = await db.Registrations.CountAsync(r => r.WorkshopId == workshopId
                && r.Status == RegistrationStatus.Confirmed, cancellationToken);
            var status = confirmed < workshop.Capacity ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted;
            if (status == RegistrationStatus.Confirmed && await HasConflictAsync(participantId, workshop, cancellationToken))
                throw new DomainException(409, "schedule_conflict", "This participant has a confirmed workshop that overlaps this time. Choose another participant or workshop.");

            var registration = new Registration
            {
                WorkshopId = workshopId, ParticipantId = participantId, Status = status,
                Created = clock.GetUtcNow().UtcDateTime
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync(cancellationToken);
            var dto = await GetDtoAsync(registration.Id, cancellationToken);
            db.IdempotencyRequests.Add(new IdempotencyRequest
            {
                Key = idempotencyKey, Fingerprint = fingerprint, RegistrationId = registration.Id,
                ResponseJson = JsonSerializer.Serialize(dto, ApiJson.Options), Created = registration.Created
            });
            await db.SaveChangesAsync(cancellationToken);
            if (status == RegistrationStatus.Confirmed)
                pendingNotifications.Add(Notification(registration, NotificationKind.Confirmed));
            return new RegistrationResult(dto, false);
        }, cancellationToken);

        await DeliverNotificationsAsync(pendingNotifications);
        return result;
    }

    public async Task<CancellationResult> CancelAsync(long registrationId, CancellationToken cancellationToken = default)
    {
        var pendingNotifications = new List<RegistrationNotification>();
        var result = await InWriteTransactionAsync(async () =>
        {
            var registration = await db.Registrations.Include(r => r.Workshop)
                .SingleOrDefaultAsync(r => r.Id == registrationId, cancellationToken)
                ?? throw new DomainException(404, "registration_not_found", "This registration could not be found.");
            if (registration.Status == RegistrationStatus.Cancelled)
                return new CancellationResult(await GetDtoAsync(registrationId, cancellationToken), null);

            var vacatesSeat = registration.Status == RegistrationStatus.Confirmed;
            registration.Status = RegistrationStatus.Cancelled;
            // Save the cancellation first so the capacity trigger sees the vacancy when promoting.
            // Both saves remain in the same transaction and become visible together at commit.
            await db.SaveChangesAsync(cancellationToken);
            pendingNotifications.Add(Notification(registration, NotificationKind.Cancelled));

            Registration? promoted = null;
            if (vacatesSeat)
            {
                var candidates = await db.Registrations.Where(r => r.WorkshopId == registration.WorkshopId
                    && r.Status == RegistrationStatus.Waitlisted).OrderBy(r => r.Created).ThenBy(r => r.Id)
                    .ToListAsync(cancellationToken);
                foreach (var candidate in candidates)
                {
                    if (await HasConflictAsync(candidate.ParticipantId, registration.Workshop, cancellationToken)) continue;
                    candidate.Status = RegistrationStatus.Confirmed;
                    promoted = candidate;
                    await db.SaveChangesAsync(cancellationToken);
                    pendingNotifications.Add(Notification(candidate, NotificationKind.Promoted));
                    break;
                }
            }

            return new CancellationResult(await GetDtoAsync(registrationId, cancellationToken),
                promoted is null ? null : await GetDtoAsync(promoted.Id, cancellationToken));
        }, cancellationToken);

        await DeliverNotificationsAsync(pendingNotifications);
        return result;
    }

    private Task<bool> HasConflictAsync(long participantId, Workshop workshop, CancellationToken ct) =>
        db.Registrations.AnyAsync(r => r.ParticipantId == participantId && r.Status == RegistrationStatus.Confirmed
            && r.Workshop.Start < workshop.End && workshop.Start < r.Workshop.End, ct);

    private Task<RegistrationDto> GetDtoAsync(long id, CancellationToken ct) =>
        db.Registrations.AsNoTracking().Where(r => r.Id == id).Select(DtoProjection.Registration).SingleAsync(ct);

    private static RegistrationNotification Notification(Registration registration, NotificationKind kind) =>
        new(registration.Id, registration.ParticipantId, registration.WorkshopId, kind);

    private async Task<T> InWriteTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            // Acquire SQLite's writer reservation BEFORE reading capacity, conflicts, duplicates or keys.
            // Every request owns an independent context/connection. No process-local lock is involved.
            await using var sqliteTransaction = connection.BeginTransaction(deferred: false);
            await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction, ct)
                ?? throw new InvalidOperationException("Unable to enlist the SQLite transaction.");
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException sqlite)
        {
            db.ChangeTracker.Clear();
            throw TranslateDatabaseException(sqlite);
        }
        catch (SqliteException exception)
        {
            db.ChangeTracker.Clear();
            throw TranslateDatabaseException(exception);
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static Exception TranslateDatabaseException(SqliteException exception)
    {
        if (exception.SqliteErrorCode is 5 or 6)
            return new DomainException(503, "database_busy", "The conference is receiving many requests. Retry this action with the same idempotency key.");
        if (exception.Message.Contains("seatflow_capacity", StringComparison.Ordinal))
            return new DomainException(409, "capacity_conflict", "The available seats changed. Refresh the workshop and retry.");
        if (exception.Message.Contains("seatflow_overlap", StringComparison.Ordinal))
            return new DomainException(409, "schedule_conflict", "This participant has an overlapping confirmed workshop.");
        if (exception.SqliteErrorCode == 19 && exception.Message.Contains("Registrations.WorkshopId", StringComparison.Ordinal))
            return new DomainException(409, "duplicate_registration", "This participant already has an active registration for this workshop.");
        return exception;
    }

    private async Task DeliverNotificationsAsync(IEnumerable<RegistrationNotification> items)
    {
        foreach (var notification in items)
        {
            try
            {
                // Request disconnects must not cancel delivery after the business operation has committed.
                await notifications.SendAsync(notification, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification delivery failed after commit for registration {RegistrationId}, event {NotificationKind}",
                    notification.RegistrationId, notification.Kind);
            }
        }
    }
}
