namespace SeatFlow.Api.Domain;

public class Workshop
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int Capacity { get; set; }

    public List<Registration> Registrations { get; set; } = [];

    /// <summary>True when the two workshops' time ranges intersect. End times are exclusive.</summary>
    public bool OverlapsWith(Workshop other) =>
        StartsAt < other.EndsAt && other.StartsAt < EndsAt;
}
