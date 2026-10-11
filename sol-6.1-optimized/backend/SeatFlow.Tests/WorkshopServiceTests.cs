using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

public sealed class WorkshopServiceTests
{
    private static CreateWorkshopRequest Request => new("Night workshop",
        new DateTimeOffset(2030, 6, 12, 23, 0, 0, TimeSpan.FromHours(2)),
        new DateTimeOffset(2030, 6, 13, 1, 0, 0, TimeSpan.FromHours(2)), 4);

    [Fact]
    public async Task Creation_replay_survives_new_contexts_and_uses_normalized_input_and_original_response()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        WorkshopCreationResult first;
        await using (var db = fixture.NewContext())
            first = await new WorkshopService(db, fixture.Clock).CreateAsync(Request, "creation-attempt");

        // State may change after creation, but a retry must replay the original result.
        var participant = await fixture.ParticipantAsync();
        await using (var db = fixture.NewContext())
        {
            var workshop = await db.Workshops.SingleAsync();
            await fixture.Service(db).RegisterAsync(workshop.Id, participant.Id, "registration-after-creation");
        }
        await using (var db = fixture.NewContext())
        {
            var equivalent = Request with
            {
                Title = "  Night workshop  ", Start = Request.Start.ToUniversalTime(), End = Request.End.ToUniversalTime()
            };
            var replay = await new WorkshopService(db, fixture.Clock).CreateAsync(equivalent, "creation-attempt");
            Assert.False(first.IsReplay);
            Assert.True(replay.IsReplay);
            Assert.Equal(first.Workshop, replay.Workshop);
            Assert.Equal(0, replay.Workshop.ConfirmedCount);
        }
        await using var fresh = fixture.NewContext();
        Assert.Single(await fresh.Workshops.ToArrayAsync());
        Assert.Single(await fresh.WorkshopCreationRequests.ToArrayAsync());
        Assert.Single(await fresh.Registrations.ToArrayAsync());
    }

    [Theory]
    [InlineData("title")]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("capacity")]
    public async Task Creation_key_is_bound_to_every_field_of_the_normalized_request(string changedField)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using (var db = fixture.NewContext())
            await new WorkshopService(db, fixture.Clock).CreateAsync(Request, "bound-creation-key");
        var changed = changedField switch
        {
            "title" => Request with { Title = "Different title" },
            "start" => Request with { Start = Request.Start.AddMinutes(1) },
            "end" => Request with { End = Request.End.AddMinutes(1) },
            _ => Request with { Capacity = Request.Capacity + 1 }
        };

        await using (var db = fixture.NewContext())
        {
            var exception = await Assert.ThrowsAsync<DomainException>(() =>
                new WorkshopService(db, fixture.Clock).CreateAsync(changed, "bound-creation-key"));
            Assert.Equal(409, exception.StatusCode);
            Assert.Equal("idempotency_key_conflict", exception.Code);
        }
        await using var fresh = fixture.NewContext();
        Assert.Single(await fresh.Workshops.ToArrayAsync());
        Assert.Single(await fresh.WorkshopCreationRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Concurrent_creation_retries_use_independent_connections_and_persist_one_workshop_and_key()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var operations = Enumerable.Range(0, 8).Select<int, Func<SeatFlowDbContext, Task<WorkshopCreationResult>>>(_ =>
            db => new WorkshopService(db, fixture.Clock).CreateAsync(Request, "contended-creation-key")).ToArray();

        var results = await fixture.RaceAsync(operations);

        Assert.Single(results, result => !result.IsReplay);
        Assert.Single(results.Select(result => result.Workshop).Distinct());
        await using var fresh = fixture.NewContext();
        var workshop = Assert.Single(await fresh.Workshops.ToArrayAsync());
        Assert.Equal(results[0].Workshop.Id, workshop.Id);
        Assert.Equal(workshop.Id, Assert.Single(await fresh.WorkshopCreationRequests.ToArrayAsync()).WorkshopId);
    }

    [Fact]
    public async Task Concurrent_same_key_with_different_creation_input_cannot_replay_an_unrelated_workshop()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var results = await fixture.RaceAsync(
            db => CaptureAsync(() => new WorkshopService(db, fixture.Clock).CreateAsync(Request, "competing-key")),
            db => CaptureAsync(() => new WorkshopService(db, fixture.Clock).CreateAsync(Request with { Title = "Other workshop" }, "competing-key")));

        Assert.Single(results, result => result is null);
        var conflict = Assert.Single(results.OfType<DomainException>());
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal("idempotency_key_conflict", conflict.Code);
        await using var fresh = fixture.NewContext();
        Assert.Single(await fresh.Workshops.ToArrayAsync());
        Assert.Single(await fresh.WorkshopCreationRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Failure_to_save_the_creation_key_rolls_back_the_workshop_too()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using (var db = fixture.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER reject_creation_key BEFORE INSERT ON WorkshopCreationRequests
                BEGIN SELECT RAISE(ABORT, 'test_key_failure'); END;
                """);
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                new WorkshopService(db, fixture.Clock).CreateAsync(Request, "failed-key-save"));
        }

        await using var fresh = fixture.NewContext();
        Assert.Empty(await fresh.Workshops.ToArrayAsync());
        Assert.Empty(await fresh.WorkshopCreationRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Existing_database_receives_additive_creation_schema_without_losing_registrations_or_retry_keys()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var workshop = await fixture.WorkshopAsync();
        var participant = await fixture.ParticipantAsync();
        var booking = await fixture.RegisterAsync(workshop, participant, "existing-registration-key");
        await using (var db = fixture.NewContext())
            await db.Database.ExecuteSqlRawAsync("DROP TABLE WorkshopCreationRequests;");

        await using (var db = fixture.NewContext())
        {
            // A database created by the previous version contains all other tables.
            await DatabaseInitializer.InitializeAsync(db, seed: true);
            await DatabaseInitializer.InitializeAsync(db, seed: true);
            var created = await new WorkshopService(db, fixture.Clock).CreateAsync(Request, "after-upgrade");
            Assert.False(created.IsReplay);
        }

        await using var fresh = fixture.NewContext();
        Assert.Equal(2, await fresh.Workshops.CountAsync());
        Assert.Equal(participant.Id, Assert.Single(await fresh.Participants.ToArrayAsync()).Id);
        Assert.Equal(booking.Registration.Id, Assert.Single(await fresh.Registrations.ToArrayAsync()).Id);
        Assert.Equal("existing-registration-key", Assert.Single(await fresh.IdempotencyRequests.ToArrayAsync()).Key);
        Assert.Single(await fresh.WorkshopCreationRequests.ToArrayAsync());
        var replay = await fixture.Service(fresh).RegisterAsync(workshop.Id, participant.Id, "existing-registration-key");
        Assert.True(replay.IsReplay);
        Assert.Equal(booking.Registration, replay.Registration);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad\nkey")]
    public async Task Invalid_creation_keys_do_not_persist_any_workshop(string key)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.NewContext();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            new WorkshopService(db, fixture.Clock).CreateAsync(Request, key));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(await db.Workshops.ToArrayAsync());
        Assert.Empty(await db.WorkshopCreationRequests.ToArrayAsync());
    }

    private static async Task<Exception?> CaptureAsync(Func<Task<WorkshopCreationResult>> operation)
    {
        try { await operation(); return null; }
        catch (DomainException exception) { return exception; }
    }
}
