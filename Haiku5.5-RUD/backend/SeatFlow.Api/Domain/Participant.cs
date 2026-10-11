namespace SeatFlow.Api.Domain;

public class Participant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}
