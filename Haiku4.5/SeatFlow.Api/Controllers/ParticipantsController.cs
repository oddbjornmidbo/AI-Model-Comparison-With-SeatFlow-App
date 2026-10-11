using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Controllers;

[ApiController]
[Route("api/participants")]
public class ParticipantsController : ControllerBase
{
    private readonly SeatFlowContext _context;

    public ParticipantsController(SeatFlowContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParticipantDto>>> GetParticipants()
    {
        var participants = await _context.Participants.ToListAsync();
        return Ok(participants.Select(p => new ParticipantDto { Id = p.Id, Name = p.Name, Email = p.Email }));
    }

    [HttpPost]
    public async Task<ActionResult<ParticipantDto>> CreateParticipant(CreateParticipantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Name and email required");

        var participant = new Participant { Name = request.Name, Email = request.Email };
        _context.Participants.Add(participant);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetParticipants), new { id = participant.Id },
            new ParticipantDto { Id = participant.Id, Name = participant.Name, Email = participant.Email });
    }
}

public class ParticipantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class CreateParticipantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
