using Microsoft.EntityFrameworkCore;

namespace SeatFlow.Core;

/// <summary>Development seed data. Demo registrations go through the service so the rules apply.</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(IDbContextFactory<SeatFlowDbContext> dbFactory, SeatFlowService service)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        if (await db.Workshops.AnyAsync())
            return;

        var day = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        db.Workshops.AddRange(
            new Workshop { Title = "Domain-Driven Design in Practice", StartTime = day.AddHours(9), EndTime = day.AddHours(11), Capacity = 2 },
            new Workshop { Title = "Event Sourcing Fundamentals", StartTime = day.AddHours(10), EndTime = day.AddHours(12), Capacity = 2 },
            new Workshop { Title = "Testing Asynchronous Code", StartTime = day.AddHours(11.5), EndTime = day.AddHours(13), Capacity = 1 },
            new Workshop { Title = "Accessible Frontends", StartTime = day.AddHours(13), EndTime = day.AddHours(15), Capacity = 3 },
            new Workshop { Title = "Lightning Talks", StartTime = day.AddHours(15), EndTime = day.AddHours(16), Capacity = 10 });

        db.Participants.AddRange(
            new Participant { Name = "Alice Hansen", Email = "alice@example.com" },
            new Participant { Name = "Bjørn Olsen", Email = "bjorn@example.com" },
            new Participant { Name = "Chen Wei", Email = "chen@example.com" },
            new Participant { Name = "Dana Berg", Email = "dana@example.com" },
            new Participant { Name = "Erik Lund", Email = "erik@example.com" },
            new Participant { Name = "Fatima Yusuf", Email = "fatima@example.com" });
        await db.SaveChangesAsync();

        // Demo state: Testing Async Code (cap 1) is full with a waitlist; DDD (cap 2) is full with a waitlist.
        var workshops = await db.Workshops.OrderBy(w => w.Id).ToListAsync();
        var participants = await db.Participants.OrderBy(p => p.Id).ToListAsync();
        int W(int index) => workshops[index].Id;
        int P(int index) => participants[index].Id;

        await service.RegisterAsync(W(2), P(0), null); // Alice confirmed
        await service.RegisterAsync(W(2), P(1), null); // Bjørn waitlisted
        await service.RegisterAsync(W(0), P(2), null); // Chen confirmed
        await service.RegisterAsync(W(0), P(3), null); // Dana confirmed
        await service.RegisterAsync(W(0), P(5), null); // Fatima waitlisted
        await service.RegisterAsync(W(1), P(4), null); // Erik confirmed
    }
}
