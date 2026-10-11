using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Services;

public static class DtoProjection
{
    public static readonly Expression<Func<Workshop, WorkshopDto>> Workshop = w => new WorkshopDto(
        w.Id, w.Title, w.Start, w.End, w.Capacity,
        w.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed),
        w.Registrations.Count(r => r.Status == RegistrationStatus.Waitlisted),
        w.Capacity - w.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed));

    public static readonly Expression<Func<Registration, RegistrationDto>> Registration = r => new RegistrationDto(
        r.Id, r.WorkshopId, r.ParticipantId, r.Status, r.Created,
        new ParticipantDto(r.Participant.Id, r.Participant.Name, r.Participant.Email),
        new WorkshopDto(r.Workshop.Id, r.Workshop.Title, r.Workshop.Start, r.Workshop.End, r.Workshop.Capacity,
            r.Workshop.Registrations.Count(item => item.Status == RegistrationStatus.Confirmed),
            r.Workshop.Registrations.Count(item => item.Status == RegistrationStatus.Waitlisted),
            r.Workshop.Capacity - r.Workshop.Registrations.Count(item => item.Status == RegistrationStatus.Confirmed)));
}

public sealed class ReadService(SeatFlowDbContext db)
{
    public Task<List<WorkshopDto>> GetWorkshopsAsync(CancellationToken ct = default) =>
        db.Workshops.AsNoTracking().OrderBy(w => w.Start).ThenBy(w => w.Id).Select(DtoProjection.Workshop).ToListAsync(ct);

    public async Task<WorkshopDto> GetWorkshopAsync(long id, CancellationToken ct = default) =>
        await db.Workshops.AsNoTracking().Where(w => w.Id == id).Select(DtoProjection.Workshop).SingleOrDefaultAsync(ct)
        ?? throw new DomainException(404, "workshop_not_found", "This workshop could not be found.");

    public Task<List<ParticipantDto>> GetParticipantsAsync(CancellationToken ct = default) =>
        db.Participants.AsNoTracking().OrderBy(p => p.Name).Select(p => new ParticipantDto(p.Id, p.Name, p.Email)).ToListAsync(ct);

    public async Task<ParticipantDto> GetParticipantAsync(long id, CancellationToken ct = default) =>
        await db.Participants.AsNoTracking().Where(p => p.Id == id).Select(p => new ParticipantDto(p.Id, p.Name, p.Email))
            .SingleOrDefaultAsync(ct)
        ?? throw new DomainException(404, "participant_not_found", "This participant could not be found.");

    public async Task<List<RegistrationDto>> GetWorkshopRegistrationsAsync(long id, CancellationToken ct = default)
    {
        await GetWorkshopAsync(id, ct);
        return await db.Registrations.AsNoTracking().Where(r => r.WorkshopId == id)
            .OrderBy(r => r.Created).ThenBy(r => r.Id).Select(DtoProjection.Registration).ToListAsync(ct);
    }

    public async Task<RegistrationDto> GetRegistrationAsync(long id, CancellationToken ct = default) =>
        await db.Registrations.AsNoTracking().Where(r => r.Id == id).Select(DtoProjection.Registration).SingleOrDefaultAsync(ct)
        ?? throw new DomainException(404, "registration_not_found", "This registration could not be found.");

    public async Task<List<RegistrationDto>> GetScheduleAsync(long participantId, CancellationToken ct = default)
    {
        await GetParticipantAsync(participantId, ct);
        return await db.Registrations.AsNoTracking().Where(r => r.ParticipantId == participantId && r.Status != RegistrationStatus.Cancelled)
            .OrderBy(r => r.Workshop.Start).ThenBy(r => r.Created).ThenBy(r => r.Id)
            .Select(DtoProjection.Registration).ToListAsync(ct);
    }
}
