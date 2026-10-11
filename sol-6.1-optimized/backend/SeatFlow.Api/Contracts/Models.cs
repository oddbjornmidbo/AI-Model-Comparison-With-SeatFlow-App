using System.Text.Json;
using System.Text.Json.Serialization;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Contracts;

public sealed record WorkshopDto(long Id, string Title, DateTime Start, DateTime End,
    int Capacity, int ConfirmedCount, int WaitlistedCount, int RemainingSeats);

public sealed record ParticipantDto(long Id, string Name, string Email);

public sealed record RegistrationDto(long Id, long WorkshopId, long ParticipantId,
    RegistrationStatus Status, DateTime Created, ParticipantDto Participant, WorkshopDto Workshop);

public sealed record RegistrationResult(RegistrationDto Registration, bool IsReplay);
public sealed record WorkshopCreationResult(WorkshopDto Workshop, bool IsReplay);
public sealed record CancellationResult(RegistrationDto Registration, RegistrationDto? PromotedRegistration);
public sealed record CreateWorkshopRequest(string? Title, DateTimeOffset Start, DateTimeOffset End, int Capacity);
public sealed record CreateRegistrationRequest(long ParticipantId);

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
