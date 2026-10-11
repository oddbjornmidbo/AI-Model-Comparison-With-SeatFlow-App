using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Contracts;

public sealed record WorkshopDto(
    int Id,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int Capacity,
    int ConfirmedCount,
    int WaitlistCount)
{
    public int RemainingCapacity => Math.Max(0, Capacity - ConfirmedCount);
}

public sealed record ParticipantDto(int Id, string Name, string Email);

public sealed record RegistrationDto(
    int Id,
    int WorkshopId,
    string WorkshopTitle,
    DateTimeOffset WorkshopStartsAt,
    DateTimeOffset WorkshopEndsAt,
    int ParticipantId,
    string ParticipantName,
    string Status,
    DateTimeOffset CreatedAt)
{
    public static RegistrationDto From(Registration r) => new(
        r.Id, r.WorkshopId, r.Workshop.Title, r.Workshop.StartsAt, r.Workshop.EndsAt,
        r.ParticipantId, r.Participant.Name, r.Status.ToString(), r.CreatedAt);
}

public sealed record CancelRegistrationResponse(RegistrationDto Registration, RegistrationDto? Promoted);

public sealed record CreateWorkshopRequest(string? Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity);

public sealed record RegisterRequest(int ParticipantId);
