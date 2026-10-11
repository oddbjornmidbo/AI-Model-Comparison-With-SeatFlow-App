namespace SeatFlow.Api.Domain;

public enum RegistrationStatus { Confirmed, Waitlisted, Cancelled }

public sealed class Workshop
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Capacity { get; set; }
    public List<Registration> Registrations { get; set; } = [];
}

public sealed class Participant
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public List<Registration> Registrations { get; set; } = [];
}

public sealed class Registration
{
    public long Id { get; set; }
    public long WorkshopId { get; set; }
    public long ParticipantId { get; set; }
    public RegistrationStatus Status { get; set; }
    public DateTime Created { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public Participant Participant { get; set; } = null!;
}

public sealed class IdempotencyRequest
{
    public string Key { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public long RegistrationId { get; set; }
    public string ResponseJson { get; set; } = "";
    public DateTime Created { get; set; }
    public Registration Registration { get; set; } = null!;
}

public sealed class WorkshopCreationRequest
{
    public string Key { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public long WorkshopId { get; set; }
    public string ResponseJson { get; set; } = "";
    public DateTime Created { get; set; }
    public Workshop Workshop { get; set; } = null!;
}
