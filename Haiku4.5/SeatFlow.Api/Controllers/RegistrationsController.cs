using Microsoft.AspNetCore.Mvc;
using SeatFlow.Api.Services;

namespace SeatFlow.Api.Controllers;

[ApiController]
[Route("api")]
public class RegistrationsController : ControllerBase
{
    private readonly RegistrationService _registrationService;

    public RegistrationsController(RegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    [HttpPost("workshops/{workshopId}/registrations")]
    public async Task<ActionResult<RegistrationDto>> Register(int workshopId, RegisterRequest request)
    {
        try
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var registration = await _registrationService.RegisterAsync(workshopId, request.ParticipantId, idempotencyKey);

            return Created($"/api/registrations/{registration.Id}", new RegistrationDto
            {
                Id = registration.Id,
                WorkshopId = registration.WorkshopId,
                ParticipantId = registration.ParticipantId,
                Status = registration.Status.ToString(),
                CreatedAt = registration.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("registrations/{id}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await _registrationService.CancelAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("participants/{id}/schedule")]
    public async Task<ActionResult<IEnumerable<ScheduleItemDto>>> GetParticipantSchedule(int id)
    {
        var registrations = await _registrationService.GetParticipantScheduleAsync(id);
        return Ok(registrations.Select(r => new ScheduleItemDto
        {
            RegistrationId = r.Id,
            WorkshopId = r.WorkshopId,
            WorkshopTitle = r.Workshop.Title,
            StartTime = r.Workshop.StartTime,
            EndTime = r.Workshop.EndTime
        }));
    }
}

public class RegisterRequest
{
    public int ParticipantId { get; set; }
}

public class ScheduleItemDto
{
    public int RegistrationId { get; set; }
    public int WorkshopId { get; set; }
    public string WorkshopTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
