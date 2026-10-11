namespace SeatFlow.Core;

public enum RegistrationStatus
{
    Confirmed,
    Waitlisted,
    Cancelled
}

/// <summary>A workshop. Times are stored in UTC.</summary>
public class Workshop
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }

    /// <summary>Half-open interval check: back-to-back workshops do not overlap.</summary>
    public bool OverlapsWith(Workshop other) =>
        StartTime < other.EndTime && other.StartTime < EndTime;
}

public class Participant
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public class Registration
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public RegistrationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Optional client-supplied Idempotency-Key; unique when present.</summary>
    public string? IdempotencyKey { get; set; }

    public Workshop Workshop { get; set; } = null!;
    public Participant Participant { get; set; } = null!;
}
