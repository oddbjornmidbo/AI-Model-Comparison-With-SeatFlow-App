namespace SeatFlow.Api;

public enum RegistrationStatus
{
    Confirmed,
    Waitlisted,
    Cancelled
}

public sealed class Workshop
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
}

public sealed class Participant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class Registration
{
    public Guid Id { get; set; }
    public Guid WorkshopId { get; set; }
    public Guid ParticipantId { get; set; }
    public RegistrationStatus Status { get; set; }
    public long QueuePosition { get; set; }
    public DateTime CreatedAt { get; set; }
    public Workshop? Workshop { get; set; }
    public Participant? Participant { get; set; }
}

public sealed class IdempotencyRecord
{
    public string Key { get; set; } = "";
    public Guid WorkshopId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid RegistrationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record WorkshopInput(string? Title, DateTimeOffset? StartTime, DateTimeOffset? EndTime, int Capacity);
public sealed record RegistrationInput(Guid ParticipantId);
public sealed record RegistrationView(Guid Id, Guid WorkshopId, Guid ParticipantId, string ParticipantName, string ParticipantEmail, RegistrationStatus Status, DateTimeOffset CreatedAt);
public sealed record WorkshopView(Guid Id, string Title, DateTimeOffset StartTime, DateTimeOffset EndTime, int Capacity, int ConfirmedCount, int RemainingCapacity);
public sealed record ScheduleItem(Guid RegistrationId, Guid WorkshopId, string WorkshopTitle, DateTimeOffset StartTime, DateTimeOffset EndTime, RegistrationStatus Status);

public sealed class DomainException(string message, int statusCode = StatusCodes.Status400BadRequest) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
