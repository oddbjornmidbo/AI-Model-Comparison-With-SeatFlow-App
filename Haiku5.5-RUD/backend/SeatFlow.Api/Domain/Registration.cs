namespace SeatFlow.Api.Domain;

public class Registration
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public RegistrationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? IdempotencyKey { get; set; }
}
