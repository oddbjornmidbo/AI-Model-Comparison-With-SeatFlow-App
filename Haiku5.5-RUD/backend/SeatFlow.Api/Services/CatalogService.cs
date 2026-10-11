using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Services;

/// <summary>Workshop and participant queries plus workshop creation.</summary>
public sealed class CatalogService(IDbContextFactory<SeatFlowDbContext> dbFactory)
{
    public async Task<IReadOnlyList<WorkshopDto>> ListWorkshopsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // SQLite cannot ORDER BY DateTimeOffset, so sorting by start time happens in memory.
        var workshops = await db.Workshops
            .Select(w => new WorkshopDto(
                w.Id, w.Title, w.StartsAt, w.EndsAt, w.Capacity,
                w.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed),
                w.Registrations.Count(r => r.Status == RegistrationStatus.Waitlisted)))
            .ToListAsync(ct);

        return workshops.OrderBy(w => w.StartsAt).ThenBy(w => w.Id).ToList();
    }

    public async Task<WorkshopDto> CreateWorkshopAsync(string title, DateTimeOffset startsAt, DateTimeOffset endsAt, int capacity, CancellationToken ct = default)
    {
        if (endsAt <= startsAt)
        {
            throw new SeatFlowException(SeatFlowErrorKind.Validation, "End time must be after start time.");
        }
        if (capacity < 1)
        {
            throw new SeatFlowException(SeatFlowErrorKind.Validation, "Capacity must be at least 1.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var workshop = new Workshop
        {
            Title = title.Trim(),
            StartsAt = startsAt.ToUniversalTime(),
            EndsAt = endsAt.ToUniversalTime(),
            Capacity = capacity
        };
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync(ct);

        return new WorkshopDto(workshop.Id, workshop.Title, workshop.StartsAt, workshop.EndsAt, workshop.Capacity, 0, 0);
    }

    public async Task<IReadOnlyList<RegistrationDto>> ListWorkshopRegistrationsAsync(int workshopId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Workshops.AnyAsync(w => w.Id == workshopId, ct))
        {
            throw new SeatFlowException(SeatFlowErrorKind.NotFound, $"Workshop {workshopId} was not found.");
        }

        // Active registrations only: confirmed seats first, then the waitlist in FIFO order.
        var rows = await db.Registrations
            .Include(r => r.Workshop)
            .Include(r => r.Participant)
            .Where(r => r.WorkshopId == workshopId && r.Status != RegistrationStatus.Cancelled)
            .ToListAsync(ct);

        return rows
            .OrderBy(r => r.Status == RegistrationStatus.Confirmed ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Select(RegistrationDto.From)
            .ToList();
    }

    public async Task<IReadOnlyList<ParticipantDto>> ListParticipantsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Participants
            .OrderBy(p => p.Name)
            .Select(p => new ParticipantDto(p.Id, p.Name, p.Email))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RegistrationDto>> GetScheduleAsync(int participantId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Participants.AnyAsync(p => p.Id == participantId, ct))
        {
            throw new SeatFlowException(SeatFlowErrorKind.NotFound, $"Participant {participantId} was not found.");
        }

        var rows = await db.Registrations
            .Include(r => r.Workshop)
            .Include(r => r.Participant)
            .Where(r => r.ParticipantId == participantId)
            .ToListAsync(ct);

        return rows
            .OrderBy(r => r.Workshop.StartsAt)
            .ThenBy(r => r.Workshop.Id)
            .Select(RegistrationDto.From)
            .ToList();
    }
}
