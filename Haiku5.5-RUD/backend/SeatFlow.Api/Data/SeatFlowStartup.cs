using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Services;

namespace SeatFlow.Api.Data;

public static class SeatFlowStartup
{
    /// <summary>Creates the schema if needed and seeds demo data into an empty database.</summary>
    public static async Task InitializeSeatFlowAsync(this IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<SeatFlowDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        if (await db.Workshops.AnyAsync())
        {
            return;
        }

        // Timestamps use Europe/Oslo (+01:00 in November). Workshops 1/2 and 4/5 partially overlap.
        var oslo = TimeSpan.FromHours(1);
        db.Workshops.AddRange(
            new Workshop { Title = "Intro to Minimal APIs", StartsAt = new(2026, 11, 10, 9, 0, 0, oslo), EndsAt = new(2026, 11, 10, 11, 0, 0, oslo), Capacity = 2 },
            new Workshop { Title = "Domain-Driven Design in Practice", StartsAt = new(2026, 11, 10, 10, 30, 0, oslo), EndsAt = new(2026, 11, 10, 12, 30, 0, oslo), Capacity = 2 },
            new Workshop { Title = "Accessibility Basics for Web Teams", StartsAt = new(2026, 11, 10, 13, 0, 0, oslo), EndsAt = new(2026, 11, 10, 15, 0, 0, oslo), Capacity = 3 },
            new Workshop { Title = "React and TypeScript Hands-on", StartsAt = new(2026, 11, 11, 9, 0, 0, oslo), EndsAt = new(2026, 11, 11, 12, 0, 0, oslo), Capacity = 1 },
            new Workshop { Title = "Testing Business Rules with xUnit", StartsAt = new(2026, 11, 11, 11, 0, 0, oslo), EndsAt = new(2026, 11, 11, 13, 0, 0, oslo), Capacity = 2 });

        db.Participants.AddRange(
            new Participant { Name = "Ada Larsen", Email = "ada@example.com" },
            new Participant { Name = "Bjørn Haugen", Email = "bjorn@example.com" },
            new Participant { Name = "Chen Li", Email = "chen@example.com" },
            new Participant { Name = "Dina Solberg", Email = "dina@example.com" },
            new Participant { Name = "Erik Vik", Email = "erik@example.com" },
            new Participant { Name = "Fatima Nour", Email = "fatima@example.com" });
        await db.SaveChangesAsync();
        await db.DisposeAsync();

        // Sample registrations go through the real service so the seeded state obeys the business rules.
        var registrations = services.GetRequiredService<RegistrationService>();
        await registrations.RegisterAsync(1, 1, null);   // Ada -> Minimal APIs (confirmed)
        await registrations.RegisterAsync(1, 2, null);   // Bjørn -> Minimal APIs (confirmed, now full)
        await registrations.RegisterAsync(1, 3, null);   // Chen -> Minimal APIs (waitlisted)
        await registrations.RegisterAsync(4, 4, null);   // Dina -> React (confirmed, now full)
        await registrations.RegisterAsync(4, 5, null);   // Erik -> React (waitlisted)
        await registrations.RegisterAsync(2, 6, null);   // Fatima -> DDD (confirmed)
    }
}
