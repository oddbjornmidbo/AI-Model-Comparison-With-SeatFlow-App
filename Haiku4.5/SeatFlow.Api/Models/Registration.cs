namespace SeatFlow.Api.Models;

public class Registration
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public RegistrationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Workshop Workshop { get; set; } = null!;
    public Participant Participant { get; set; } = null!;
}
