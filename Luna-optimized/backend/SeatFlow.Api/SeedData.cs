using Microsoft.EntityFrameworkCore;

namespace SeatFlow.Api;

public static class SeedData
{
    public static async Task InitializeAsync(SeatFlowDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (await db.Participants.AnyAsync(cancellationToken)) return;

        var start = DateTime.UtcNow.Date.AddDays(2);
        var participants = new[]
        {
            new Participant { Id = Guid.NewGuid(), Name = "Alex Morgan", Email = "alex@example.com" },
            new Participant { Id = Guid.NewGuid(), Name = "Jamie Chen", Email = "jamie@example.com" },
            new Participant { Id = Guid.NewGuid(), Name = "Sam Rivera", Email = "sam@example.com" },
            new Participant { Id = Guid.NewGuid(), Name = "Taylor Brooks", Email = "taylor@example.com" },
            new Participant { Id = Guid.NewGuid(), Name = "Casey Patel", Email = "casey@example.com" },
            new Participant { Id = Guid.NewGuid(), Name = "Jordan Lee", Email = "jordan@example.com" }
        };
        var workshops = new[]
        {
            new Workshop { Id = Guid.NewGuid(), Title = "Designing for the real world", StartTime = start.AddHours(9), EndTime = start.AddHours(10), Capacity = 2 },
            new Workshop { Id = Guid.NewGuid(), Title = "Stories with data", StartTime = start.AddHours(9.5), EndTime = start.AddHours(10.5), Capacity = 1 },
            new Workshop { Id = Guid.NewGuid(), Title = "Creative systems in practice", StartTime = start.AddHours(9.25), EndTime = start.AddHours(9.75), Capacity = 4 },
            new Workshop { Id = Guid.NewGuid(), Title = "A practical AI toolkit", StartTime = start.AddHours(11), EndTime = start.AddHours(12), Capacity = 3 },
            new Workshop { Id = Guid.NewGuid(), Title = "Building better teams", StartTime = start.AddHours(10), EndTime = start.AddHours(11), Capacity = 2 }
        };
        db.Participants.AddRange(participants);
        db.Workshops.AddRange(workshops);
        var createdAt = DateTime.UtcNow.AddMinutes(-15);
        db.Registrations.AddRange(
            new Registration { Id = Guid.NewGuid(), WorkshopId = workshops[0].Id, ParticipantId = participants[0].Id, Status = RegistrationStatus.Confirmed, QueuePosition = 1, CreatedAt = createdAt },
            new Registration { Id = Guid.NewGuid(), WorkshopId = workshops[0].Id, ParticipantId = participants[1].Id, Status = RegistrationStatus.Confirmed, QueuePosition = 2, CreatedAt = createdAt.AddMinutes(1) },
            new Registration { Id = Guid.NewGuid(), WorkshopId = workshops[1].Id, ParticipantId = participants[2].Id, Status = RegistrationStatus.Confirmed, QueuePosition = 1, CreatedAt = createdAt.AddMinutes(2) },
            new Registration { Id = Guid.NewGuid(), WorkshopId = workshops[1].Id, ParticipantId = participants[0].Id, Status = RegistrationStatus.Waitlisted, QueuePosition = 2, CreatedAt = createdAt.AddMinutes(3) },
            new Registration { Id = Guid.NewGuid(), WorkshopId = workshops[1].Id, ParticipantId = participants[3].Id, Status = RegistrationStatus.Waitlisted, QueuePosition = 3, CreatedAt = createdAt.AddMinutes(4) }
        );
        await db.SaveChangesAsync(cancellationToken);
    }
}
