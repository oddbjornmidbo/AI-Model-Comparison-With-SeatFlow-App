namespace SeatFlow.Api.Models;

public class Workshop
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
