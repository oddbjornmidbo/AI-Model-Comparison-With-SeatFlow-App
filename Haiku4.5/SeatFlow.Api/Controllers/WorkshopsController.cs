using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Controllers;

[ApiController]
[Route("api/workshops")]
public class WorkshopsController : ControllerBase
{
    private readonly SeatFlowContext _context;

    public WorkshopsController(SeatFlowContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkshopDto>>> GetWorkshops()
    {
        var workshops = await _context.Workshops.ToListAsync();
        return Ok(workshops.Select(w => new WorkshopDto
        {
            Id = w.Id,
            Title = w.Title,
            StartTime = w.StartTime,
            EndTime = w.EndTime,
            Capacity = w.Capacity,
            ConfirmedCount = _context.Registrations.Count(r => r.WorkshopId == w.Id && r.Status == RegistrationStatus.Confirmed),
            WaitlistCount = _context.Registrations.Count(r => r.WorkshopId == w.Id && r.Status == RegistrationStatus.Waitlisted)
        }));
    }

    [HttpPost]
    public async Task<ActionResult<WorkshopDto>> CreateWorkshop(CreateWorkshopRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Capacity < 1)
            return BadRequest("Invalid workshop data");

        var workshop = new Workshop
        {
            Title = request.Title,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Capacity = request.Capacity
        };

        _context.Workshops.Add(workshop);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWorkshops), new { id = workshop.Id }, new WorkshopDto
        {
            Id = workshop.Id,
            Title = workshop.Title,
            StartTime = workshop.StartTime,
            EndTime = workshop.EndTime,
            Capacity = workshop.Capacity,
            ConfirmedCount = 0,
            WaitlistCount = 0
        });
    }

    [HttpGet("{id}/registrations")]
    public async Task<ActionResult<IEnumerable<RegistrationDto>>> GetWorkshopRegistrations(int id)
    {
        var workshop = await _context.Workshops.FindAsync(id);
        if (workshop == null)
            return NotFound();

        var registrations = await _context.Registrations
            .Where(r => r.WorkshopId == id)
            .Include(r => r.Participant)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        return Ok(registrations.Select(r => new RegistrationDto
        {
            Id = r.Id,
            WorkshopId = r.WorkshopId,
            ParticipantId = r.ParticipantId,
            ParticipantName = r.Participant.Name,
            ParticipantEmail = r.Participant.Email,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt
        }));
    }
}

public class WorkshopDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
    public int ConfirmedCount { get; set; }
    public int WaitlistCount { get; set; }
}

public class CreateWorkshopRequest
{
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
}

public class RegistrationDto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
