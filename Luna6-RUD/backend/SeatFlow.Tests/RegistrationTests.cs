using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeatFlow.Api;
using Xunit;

namespace SeatFlow.Tests;

public sealed class RegistrationTests
{
    private static async Task<Harness> CreateAsync(int capacity = 1)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SeatFlowDbContext>().UseSqlite(connection).Options;
        var factory = new TestFactory(options);
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        db.Workshops.AddRange(
            new Workshop { Id = 1, Title = "Target", StartsAt = Day(9), EndsAt = Day(10), Capacity = capacity },
            new Workshop { Id = 2, Title = "Overlap", StartsAt = Day(9.5), EndsAt = Day(11), Capacity = 10 },
            new Workshop { Id = 3, Title = "Later", StartsAt = Day(11), EndsAt = Day(12), Capacity = 10 });
        db.Participants.AddRange(Enumerable.Range(1, 8).Select(i => new Participant { Id = i, Name = $"Person {i}", Email = $"p{i}@test.local" }));
        await db.SaveChangesAsync();
        return new Harness(connection, factory, new RecordingNotifications());
    }

    private static DateTimeOffset Day(double hour) => new(2026, 11, 1, (int)hour, (int)((hour % 1) * 60), 0, TimeSpan.Zero);
    private static RegistrationService Service(Harness h) => new(h.Factory, h.Notifications);
    private static async Task<Registration> Register(Harness h, int participant, string? key = null, int workshop = 1) => (await Service(h).RegisterAsync(workshop, participant, key, default)).Registration;

    [Fact]
    public async Task Registration_into_available_workshop_is_confirmed() { await using var h = await CreateAsync(); Assert.Equal(RegistrationStatus.Confirmed, (await Register(h, 1)).Status); }

    [Fact]
    public async Task Registration_into_full_workshop_is_waitlisted()
    {
        await using var h = await CreateAsync(); await Register(h, 1);
        Assert.Equal(RegistrationStatus.Waitlisted, (await Register(h, 2)).Status);
    }

    [Fact]
    public async Task Cancelling_confirmed_registration_promotes_oldest_waitlist_entry()
    {
        await using var h = await CreateAsync(); var a = await Register(h, 1); var b = await Register(h, 2); var c = await Register(h, 3);
        await Service(h).CancelAsync(a.Id, default);
        await using var db = await h.Factory.CreateDbContextAsync();
        Assert.Equal(RegistrationStatus.Confirmed, (await db.Registrations.FindAsync(b.Id))!.Status);
        Assert.Equal(RegistrationStatus.Waitlisted, (await db.Registrations.FindAsync(c.Id))!.Status);
    }

    [Fact]
    public async Task Promotion_skips_waitlisted_participant_with_schedule_conflict()
    {
        await using var h = await CreateAsync(); var occupant = await Register(h, 1); var first = await Register(h, 2); var eligible = await Register(h, 3);
        await Register(h, 2, workshop: 2);
        await Service(h).CancelAsync(occupant.Id, default);
        await using var db = await h.Factory.CreateDbContextAsync();
        Assert.Equal(RegistrationStatus.Waitlisted, (await db.Registrations.FindAsync(first.Id))!.Status);
        Assert.Equal(RegistrationStatus.Confirmed, (await db.Registrations.FindAsync(eligible.Id))!.Status);
    }

    [Fact]
    public async Task New_confirmation_is_rejected_if_it_overlaps_existing_confirmed_workshop()
    {
        await using var h = await CreateAsync(2); await Register(h, 1, workshop: 2);
        var ex = await Assert.ThrowsAsync<SeatFlowException>(() => Register(h, 1, workshop: 1));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_registration_is_prevented()
    {
        await using var h = await CreateAsync(); await Register(h, 1);
        await Assert.ThrowsAsync<SeatFlowException>(() => Register(h, 1));
    }

    [Fact]
    public async Task Idempotency_key_replay_returns_same_registration_without_duplicate()
    {
        await using var h = await CreateAsync(); var first = await Service(h).RegisterAsync(1, 1, "request-1", default);
        var replay = await Service(h).RegisterAsync(1, 1, "request-1", default);
        Assert.True(first.Created); Assert.False(replay.Created); Assert.Equal(first.Registration.Id, replay.Registration.Id);
        await using var db = await h.Factory.CreateDbContextAsync(); Assert.Equal(1, await db.Registrations.CountAsync());
    }

    [Fact]
    public async Task Reusing_idempotency_key_for_different_payload_returns_conflict()
    {
        await using var h = await CreateAsync(); await Service(h).RegisterAsync(1, 1, "shared", default);
        var ex = await Assert.ThrowsAsync<SeatFlowException>(() => Service(h).RegisterAsync(1, 2, "shared", default)); Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Cancelling_twice_is_idempotent()
    {
        await using var h = await CreateAsync(); var registration = await Register(h, 1);
        await Service(h).CancelAsync(registration.Id, default); var again = await Service(h).CancelAsync(registration.Id, default);
        Assert.Equal(RegistrationStatus.Cancelled, again.Status);
        Assert.Equal(1, h.Notifications.Messages.Count(m => m.Contains("was cancelled", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Concurrent_attempts_for_final_seat_confirm_only_one()
    {
        await using var h = await CreateAsync();
        var results = await Task.WhenAll(Service(h).RegisterAsync(1, 1, null, default), Service(h).RegisterAsync(1, 2, null, default));
        Assert.Single(results, x => x.Registration.Status == RegistrationStatus.Confirmed);
        Assert.Single(results, x => x.Registration.Status == RegistrationStatus.Waitlisted);
    }

    [Fact]
    public async Task Notification_failure_does_not_undo_registration()
    {
        await using var h = await CreateAsync();
        var service = new RegistrationService(h.Factory, new FailingNotifications());
        var registration = await service.RegisterAsync(1, 1, null, default);
        await using var db = await h.Factory.CreateDbContextAsync(); Assert.NotNull(await db.Registrations.FindAsync(registration.Registration.Id));
    }

    private sealed class TestFactory(DbContextOptions<SeatFlowDbContext> options) : IDbContextFactory<SeatFlowDbContext>
    {
        public SeatFlowDbContext CreateDbContext() => new(options);
        public Task<SeatFlowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
    private sealed class RecordingNotifications : INotificationService
    {
        public List<string> Messages { get; } = [];
        public Task NotifyAsync(string message, CancellationToken cancellationToken = default) { Messages.Add(message); return Task.CompletedTask; }
    }
    private sealed class FailingNotifications : INotificationService
    {
        public Task NotifyAsync(string message, CancellationToken cancellationToken = default) => throw new InvalidOperationException("notification unavailable");
    }
    private sealed class Harness(SqliteConnection connection, TestFactory factory, RecordingNotifications notifications) : IAsyncDisposable
    {
        public TestFactory Factory { get; } = factory;
        public RecordingNotifications Notifications { get; } = notifications;
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
