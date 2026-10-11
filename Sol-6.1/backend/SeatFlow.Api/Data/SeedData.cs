using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(SeatFlowDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Workshops.AnyAsync() || await db.Participants.AnyAsync())
            return;

        var day = DateTime.UtcNow.Date.AddDays(1);
        var workshops = new[]
        {
            new Workshop { Title = "Building useful APIs", StartTime = day.AddHours(9), EndTime = day.AddHours(11), Capacity = 2 },
            new Workshop { Title = "Designing better interfaces", StartTime = day.AddHours(10), EndTime = day.AddHours(12), Capacity = 3 },
            new Workshop { Title = "Practical data modeling", StartTime = day.AddHours(13), EndTime = day.AddHours(14.5), Capacity = 3 },
            new Workshop { Title = "Debugging as a team", StartTime = day.AddHours(14.5), EndTime = day.AddHours(16), Capacity = 2 }
        };
        var participants = new[]
        {
            new Participant { Name = "Alex Chen", Email = "alex@example.com" },
            new Participant { Name = "Jamie Patel", Email = "jamie@example.com" },
            new Participant { Name = "Sam Rivera", Email = "sam@example.com" },
            new Participant { Name = "Riley Olsen", Email = "riley@example.com" },
            new Participant { Name = "Morgan Lee", Email = "morgan@example.com" },
            new Participant { Name = "Nora Hansen", Email = "nora@example.com" }
        };
        db.Workshops.AddRange(workshops);
        db.Participants.AddRange(participants);
        db.Registrations.AddRange(
            new Registration { Workshop = workshops[0], Participant = participants[0], Status = RegistrationStatus.Confirmed, CreatedAt = day.AddDays(-1) },
            new Registration { Workshop = workshops[0], Participant = participants[1], Status = RegistrationStatus.Confirmed, CreatedAt = day.AddDays(-1).AddMinutes(1) },
            new Registration { Workshop = workshops[0], Participant = participants[2], Status = RegistrationStatus.Waitlisted, CreatedAt = day.AddDays(-1).AddMinutes(2) },
            new Registration { Workshop = workshops[1], Participant = participants[2], Status = RegistrationStatus.Confirmed, CreatedAt = day.AddDays(-1).AddMinutes(3) },
            new Registration { Workshop = workshops[0], Participant = participants[3], Status = RegistrationStatus.Waitlisted, CreatedAt = day.AddDays(-1).AddMinutes(4) },
            new Registration { Workshop = workshops[2], Participant = participants[4], Status = RegistrationStatus.Confirmed, CreatedAt = day.AddDays(-1).AddMinutes(5) });
        await db.SaveChangesAsync();
    }
}
