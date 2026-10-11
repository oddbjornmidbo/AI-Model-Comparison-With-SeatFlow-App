namespace SeatFlow.Api.Models;

public enum RegistrationStatus { Confirmed, Waitlisted, Cancelled }

public sealed class Workshop
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
}

public sealed class Participant
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class Registration
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public RegistrationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class IdempotencyRecord
{
    public string Key { get; set; } = "";
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public int RegistrationId { get; set; }
    public Registration Registration { get; set; } = null!;
}
