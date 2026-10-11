using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;
using SeatFlow.Api.Services;

namespace SeatFlow.Api;

[ApiController]
public sealed class ApiController(SeatFlowDbContext db, RegistrationService registrations) : ControllerBase
{
    [HttpGet("/api/workshops")]
    public async Task<ActionResult<IReadOnlyList<WorkshopDto>>> GetWorkshops(CancellationToken cancellationToken)
    {
        var items = await WorkshopQuery(db.Workshops.OrderBy(w => w.StartTime).ThenBy(w => w.Id))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("/api/workshops/{id:int}")]
    public async Task<ActionResult<WorkshopDto>> GetWorkshop(int id, CancellationToken cancellationToken)
    {
        var workshop = await WorkshopQuery(db.Workshops.Where(w => w.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(404, "WorkshopNotFound", "Workshop was not found.");
        return Ok(workshop);
    }

    [HttpPost("/api/workshops")]
    public async Task<ActionResult<WorkshopDto>> CreateWorkshop(CreateWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        var workshop = new Workshop
        {
            Title = request.Title.Trim(), StartTime = request.StartTime!.Value.UtcDateTime,
            EndTime = request.EndTime!.Value.UtcDateTime, Capacity = request.Capacity
        };
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetWorkshop), new { id = workshop.Id },
            new WorkshopDto(workshop.Id, workshop.Title, workshop.StartTime, workshop.EndTime,
                workshop.Capacity, 0, workshop.Capacity, 0));
    }

    [HttpGet("/api/participants")]
    public async Task<ActionResult<IReadOnlyList<ParticipantDto>>> GetParticipants(CancellationToken cancellationToken) =>
        Ok(await db.Participants.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new ParticipantDto(p.Id, p.Name, p.Email)).ToListAsync(cancellationToken));

    [HttpGet("/api/workshops/{id:int}/registrations")]
    public async Task<ActionResult<IReadOnlyList<RegistrationDto>>> GetRegistrations(int id,
        CancellationToken cancellationToken)
    {
        if (!await db.Workshops.AnyAsync(w => w.Id == id, cancellationToken))
            throw new ApiException(404, "WorkshopNotFound", "Workshop was not found.");
        return Ok(await db.Registrations.AsNoTracking().Where(r => r.WorkshopId == id)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Select(r => new RegistrationDto(r.Id, r.WorkshopId, r.ParticipantId,
                r.Participant.Name, r.Participant.Email, r.Status, r.CreatedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("/api/registrations/{id:int}")]
    public async Task<ActionResult<RegistrationDto>> GetRegistration(int id, CancellationToken cancellationToken)
    {
        var registration = await db.Registrations.AsNoTracking().Include(r => r.Participant)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(404, "RegistrationNotFound", "Registration was not found.");
        return Ok(RegistrationDto.From(registration));
    }

    [HttpPost("/api/workshops/{id:int}/registrations")]
    public async Task<ActionResult<RegistrationDto>> Register(int id, CreateRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var headers = Request.Headers["Idempotency-Key"];
        if (headers.Count > 1)
            throw new ApiException(400, "InvalidIdempotencyKey", "Supply only one Idempotency-Key header.");
        var registration = await registrations.RegisterAsync(id, request.ParticipantId,
            headers.Count == 0 ? null : headers[0], cancellationToken);
        return CreatedAtAction(nameof(GetRegistration), new { id = registration.Id }, registration);
    }

    [HttpPost("/api/registrations/{id:int}/cancel")]
    public async Task<ActionResult<RegistrationDto>> Cancel(int id, CancellationToken cancellationToken) =>
        Ok(await registrations.CancelAsync(id, cancellationToken));

    [HttpGet("/api/participants/{id:int}/schedule")]
    public async Task<ActionResult<IReadOnlyList<ScheduleDto>>> GetSchedule(int id, CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == id, cancellationToken))
            throw new ApiException(404, "ParticipantNotFound", "Participant was not found.");
        return Ok(await db.Registrations.AsNoTracking()
            .Where(r => r.ParticipantId == id && r.Status == RegistrationStatus.Confirmed)
            .OrderBy(r => r.Workshop.StartTime).ThenBy(r => r.WorkshopId)
            .Select(r => new ScheduleDto(r.Id, r.WorkshopId, r.Workshop.Title,
                r.Workshop.StartTime, r.Workshop.EndTime)).ToListAsync(cancellationToken));
    }

    private IQueryable<WorkshopDto> WorkshopQuery(IQueryable<Workshop> source) => source.AsNoTracking().Select(w =>
        new WorkshopDto(w.Id, w.Title, w.StartTime, w.EndTime, w.Capacity,
            db.Registrations.Count(r => r.WorkshopId == w.Id && r.Status == RegistrationStatus.Confirmed),
            w.Capacity - db.Registrations.Count(r => r.WorkshopId == w.Id && r.Status == RegistrationStatus.Confirmed),
            db.Registrations.Count(r => r.WorkshopId == w.Id && r.Status == RegistrationStatus.Waitlisted)));
}
