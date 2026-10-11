using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;

namespace SeatFlow.Tests;

public sealed class SeedDataTests
{
    [Fact]
    public async Task NewArrivalJoinsAfterExistingSeededWaitlist()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using (var db = fixture.NewContext())
        {
            await DatabaseInitializer.InitializeAsync(db, seed: true);
            Assert.All(await db.Registrations.ToArrayAsync(), row => Assert.True(row.Created <= DateTime.UtcNow));
        }
        fixture.Clock = TimeProvider.System;
        await using (var db = fixture.NewContext())
            await fixture.Service(db).RegisterAsync(2, 6, "seed-new-arrival");
        await using (var db = fixture.NewContext())
        {
            var queue = await db.Registrations.Where(row => row.WorkshopId == 2 && row.Status == RegistrationStatus.Waitlisted)
                .OrderBy(row => row.Created).ThenBy(row => row.Id).Select(row => row.ParticipantId).ToArrayAsync();
            Assert.Equal(new long[] { 1, 5, 6 }, queue);
        }
    }
}
