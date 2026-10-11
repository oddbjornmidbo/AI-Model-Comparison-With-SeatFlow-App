using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SeatFlow.Api;

public sealed class RegistrationService(SeatFlowDbContext db, INotificationService notifications, ILogger<RegistrationService> logger)
{
    public async Task<IReadOnlyList<WorkshopView>> GetWorkshopsAsync(CancellationToken cancellationToken)
    {
        var workshops = await db.Workshops.AsNoTracking().OrderBy(x => x.StartTime).ToListAsync(cancellationToken);
        var counts = await db.Registrations.AsNoTracking()
            .Where(x => x.Status == RegistrationStatus.Confirmed)
            .GroupBy(x => x.WorkshopId)
            .Select(group => new { WorkshopId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.WorkshopId, x => x.Count, cancellationToken);
        return workshops.Select(workshop =>
        {
            var count = counts.GetValueOrDefault(workshop.Id);
            return new WorkshopView(workshop.Id, workshop.Title, AsUtcOffset(workshop.StartTime), AsUtcOffset(workshop.EndTime), workshop.Capacity, count, Math.Max(0, workshop.Capacity - count));
        }).ToList();
    }

    public async Task<WorkshopView?> GetWorkshopAsync(Guid id, CancellationToken cancellationToken) =>
        (await GetWorkshopsAsync(cancellationToken)).FirstOrDefault(x => x.Id == id);

    public async Task<WorkshopView> CreateWorkshopAsync(WorkshopInput input, CancellationToken cancellationToken)
    {
        var title = input.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("A workshop title is required.");
        if (title.Length > 160) throw new DomainException("The workshop title must be 160 characters or fewer.");
        if (input.StartTime is null || input.EndTime is null) throw new DomainException("Start and end times are required.");
        var start = input.StartTime.Value.UtcDateTime;
        var end = input.EndTime.Value.UtcDateTime;
        if (end <= start) throw new DomainException("The end time must be after the start time.");
        if (input.Capacity <= 0) throw new DomainException("Capacity must be a positive whole number.");

        var workshop = new Workshop { Id = Guid.NewGuid(), Title = title, StartTime = start, EndTime = end, Capacity = input.Capacity };
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync(cancellationToken);
        return new WorkshopView(workshop.Id, workshop.Title, AsUtcOffset(workshop.StartTime), AsUtcOffset(workshop.EndTime), workshop.Capacity, 0, workshop.Capacity);
    }

    public async Task<IReadOnlyList<RegistrationView>> GetWorkshopRegistrationsAsync(Guid workshopId, CancellationToken cancellationToken)
    {
        if (!await db.Workshops.AnyAsync(x => x.Id == workshopId, cancellationToken)) throw new DomainException("Workshop not found.", StatusCodes.Status404NotFound);
        var registrations = await db.Registrations.AsNoTracking().Include(x => x.Participant)
            .Where(x => x.WorkshopId == workshopId)
            .OrderBy(x => x.Status).ThenBy(x => x.QueuePosition).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return registrations.Select(ToView).ToList();
    }

    public async Task<RegistrationView?> GetRegistrationAsync(Guid id, CancellationToken cancellationToken)
    {
        var registration = await db.Registrations.AsNoTracking().Include(x => x.Participant).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return registration is null ? null : ToView(registration);
    }

    public async Task<RegistrationView> RegisterAsync(Guid workshopId, Guid participantId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            throw new DomainException("Provide an Idempotency-Key header of 1 to 200 characters.");

        var notices = new List<SeatFlowNotification>();
        Registration result;
        await using (var transaction = await BeginImmediateTransactionAsync(cancellationToken))
        {
            var prior = await db.IdempotencyRecords.FirstOrDefaultAsync(x => x.Key == idempotencyKey, cancellationToken);
            if (prior is not null)
            {
                if (prior.WorkshopId != workshopId || prior.ParticipantId != participantId)
                    throw new DomainException("This Idempotency-Key was already used for a different request.", StatusCodes.Status409Conflict);
                var priorRegistration = await db.Registrations.Include(x => x.Participant).FirstOrDefaultAsync(x => x.Id == prior.RegistrationId, cancellationToken);
                if (priorRegistration is null) throw new DomainException("The original registration is no longer available.", StatusCodes.Status409Conflict);
                result = priorRegistration;
                await transaction.CommitAsync(cancellationToken);
                return ToView(result);
            }

            var workshop = await db.Workshops.FirstOrDefaultAsync(x => x.Id == workshopId, cancellationToken)
                ?? throw new DomainException("Workshop not found.", StatusCodes.Status404NotFound);
            var participant = await db.Participants.FirstOrDefaultAsync(x => x.Id == participantId, cancellationToken)
                ?? throw new DomainException("Participant not found.", StatusCodes.Status404NotFound);
            var existing = await db.Registrations.FirstOrDefaultAsync(x => x.WorkshopId == workshopId && x.ParticipantId == participantId && x.Status != RegistrationStatus.Cancelled, cancellationToken);
            if (existing is not null) throw new DomainException("This participant already has an active registration for this workshop.", StatusCodes.Status409Conflict);

            var confirmedCount = await db.Registrations.CountAsync(x => x.WorkshopId == workshopId && x.Status == RegistrationStatus.Confirmed, cancellationToken);
            var status = confirmedCount < workshop.Capacity ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted;
            if (status == RegistrationStatus.Confirmed && await HasScheduleConflictAsync(participantId, workshop, cancellationToken))
                throw new DomainException("This workshop overlaps a confirmed workshop in the participant’s schedule.", StatusCodes.Status409Conflict);

            result = new Registration
            {
                Id = Guid.NewGuid(), WorkshopId = workshopId, ParticipantId = participantId,
                Status = status,
                QueuePosition = (await db.Registrations.Where(x => x.WorkshopId == workshopId).Select(x => (long?)x.QueuePosition).MaxAsync(cancellationToken) ?? 0) + 1,
                CreatedAt = DateTime.UtcNow, Participant = participant, Workshop = workshop
            };
            db.Registrations.Add(result);
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = idempotencyKey, WorkshopId = workshopId, ParticipantId = participantId,
                RegistrationId = result.Id, CreatedAt = result.CreatedAt
            });
            await db.SaveChangesAsync(cancellationToken);
            if (status == RegistrationStatus.Confirmed)
                notices.Add(new SeatFlowNotification("Confirmed", result.Id, workshopId, participantId, result.CreatedAt));
            await transaction.CommitAsync(cancellationToken);
        }

        await SendNotificationsSafelyAsync(notices, cancellationToken);
        return ToView(result);
    }

    public async Task<RegistrationView> CancelAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        var notices = new List<SeatFlowNotification>();
        Registration result;
        await using (var transaction = await BeginImmediateTransactionAsync(cancellationToken))
        {
            result = await db.Registrations.Include(x => x.Participant).Include(x => x.Workshop)
                .FirstOrDefaultAsync(x => x.Id == registrationId, cancellationToken)
                ?? throw new DomainException("Registration not found.", StatusCodes.Status404NotFound);
            if (result.Status == RegistrationStatus.Cancelled)
            {
                await transaction.CommitAsync(cancellationToken);
                return ToView(result);
            }

            var wasConfirmed = result.Status == RegistrationStatus.Confirmed;
            result.Status = RegistrationStatus.Cancelled;
            await db.SaveChangesAsync(cancellationToken);
            var now = DateTime.UtcNow;
            notices.Add(new SeatFlowNotification("Cancelled", result.Id, result.WorkshopId, result.ParticipantId, now));

            if (wasConfirmed)
            {
                var waitlisted = await db.Registrations.Include(x => x.Participant)
                    .Where(x => x.WorkshopId == result.WorkshopId && x.Status == RegistrationStatus.Waitlisted)
                    .OrderBy(x => x.QueuePosition).ThenBy(x => x.Id).ToListAsync(cancellationToken);
                foreach (var candidate in waitlisted)
                {
                    if (await HasScheduleConflictAsync(candidate.ParticipantId, result.Workshop!, cancellationToken)) continue;
                    candidate.Status = RegistrationStatus.Confirmed;
                    await db.SaveChangesAsync(cancellationToken);
                    notices.Add(new SeatFlowNotification("Promoted", candidate.Id, candidate.WorkshopId, candidate.ParticipantId, now));
                    break;
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        await SendNotificationsSafelyAsync(notices, cancellationToken);
        return ToView(result);
    }

    public async Task<IReadOnlyList<ScheduleItem>> GetScheduleAsync(Guid participantId, CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(x => x.Id == participantId, cancellationToken)) throw new DomainException("Participant not found.", StatusCodes.Status404NotFound);
        var registrations = await db.Registrations.AsNoTracking()
            .Where(x => x.ParticipantId == participantId && x.Status == RegistrationStatus.Confirmed)
            .Join(db.Workshops.AsNoTracking(), registration => registration.WorkshopId, workshop => workshop.Id,
                (registration, workshop) => new
                {
                    RegistrationId = registration.Id,
                    WorkshopId = workshop.Id,
                    WorkshopTitle = workshop.Title,
                    workshop.StartTime,
                    workshop.EndTime,
                    registration.Status
                })
            .OrderBy(x => x.StartTime)
            .Select(x => new { x.RegistrationId, x.WorkshopId, x.WorkshopTitle, x.StartTime, x.EndTime, x.Status })
            .ToListAsync(cancellationToken);
        return registrations.Select(x => new ScheduleItem(x.RegistrationId, x.WorkshopId, x.WorkshopTitle, AsUtcOffset(x.StartTime), AsUtcOffset(x.EndTime), x.Status)).ToList();
    }

    private async Task<bool> HasScheduleConflictAsync(Guid participantId, Workshop candidate, CancellationToken cancellationToken)
    {
        return await db.Registrations.AsNoTracking()
            .Where(x => x.ParticipantId == participantId && x.Status == RegistrationStatus.Confirmed && x.WorkshopId != candidate.Id)
            .Join(db.Workshops.AsNoTracking(), registration => registration.WorkshopId, workshop => workshop.Id,
                (registration, workshop) => workshop)
            .AnyAsync(workshop => candidate.StartTime < workshop.EndTime && candidate.EndTime > workshop.StartTime, cancellationToken);
    }

    private async Task SendNotificationsSafelyAsync(IEnumerable<SeatFlowNotification> notices, CancellationToken cancellationToken)
    {
        foreach (var notice in notices)
        {
            try { await notifications.SendAsync(notice, cancellationToken); }
            catch (Exception exception) { logger.LogWarning(exception, "Notification delivery failed for registration {RegistrationId}.", notice.RegistrationId); }
        }
    }

    private async Task<SqliteTransaction> BeginImmediateTransactionAsync(CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var transaction = connection.BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        return transaction;
    }

    private static RegistrationView ToView(Registration registration)
    {
        var participant = registration.Participant!;
        return new RegistrationView(registration.Id, registration.WorkshopId, registration.ParticipantId, participant.Name, participant.Email, registration.Status, AsUtcOffset(registration.CreatedAt));
    }

    private static DateTimeOffset AsUtcOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
