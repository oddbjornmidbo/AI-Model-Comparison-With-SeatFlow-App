using System.ComponentModel.DataAnnotations;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Contracts;

public sealed record WorkshopDto(int Id, string Title, DateTime StartTime, DateTime EndTime,
    int Capacity, int ConfirmedCount, int RemainingCapacity, int WaitlistedCount);
public sealed record ParticipantDto(int Id, string Name, string Email);
public sealed record RegistrationDto(int Id, int WorkshopId, int ParticipantId,
    string ParticipantName, string ParticipantEmail, RegistrationStatus Status, DateTime CreatedAt)
{
    public static RegistrationDto From(Registration registration) => new(
        registration.Id, registration.WorkshopId, registration.ParticipantId,
        registration.Participant.Name, registration.Participant.Email,
        registration.Status, registration.CreatedAt);
}
public sealed record ScheduleDto(int RegistrationId, int WorkshopId, string Title,
    DateTime StartTime, DateTime EndTime);
public sealed record CreateRegistrationRequest([Range(1, int.MaxValue)] int ParticipantId);

public sealed class CreateWorkshopRequest : IValidatableObject
{
    [Required, StringLength(200)]
    public string Title { get; init; } = "";
    [Required]
    public DateTimeOffset? StartTime { get; init; }
    [Required]
    public DateTimeOffset? EndTime { get; init; }
    [Range(1, 10000)]
    public int Capacity { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Title))
            yield return new ValidationResult("Title must contain text.", [nameof(Title)]);
        if (StartTime.HasValue && EndTime.HasValue && EndTime <= StartTime)
            yield return new ValidationResult("End time must be after start time.", [nameof(EndTime)]);
    }
}
