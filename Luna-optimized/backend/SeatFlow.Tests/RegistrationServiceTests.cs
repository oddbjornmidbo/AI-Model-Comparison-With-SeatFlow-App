using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeatFlow.Api;
using Xunit;

namespace SeatFlow.Tests;

public sealed class RegistrationServiceTests
{
    [Fact]
    public async Task Registers_into_available_workshop_and_notifies()
    {
        await using var store = await TestStore.CreateAsync(capacity: 2);
        var view = await store.RegisterAsync(store.WorkshopA, store.Alex, "available-1");
        Assert.Equal(RegistrationStatus.Confirmed, view.Status);
        Assert.Single(store.Notifier.Sent);
        Assert.Equal("Confirmed", store.Notifier.Sent[0].Type);
    }

    [Fact]
    public async Task Registers_into_full_workshop_as_waitlisted()
    {
        await using var store = await TestStore.CreateAsync(capacity: 1);
        await store.RegisterAsync(store.WorkshopA, store.Alex, "full-first");
        var second = await store.RegisterAsync(store.WorkshopA, store.Jamie, "full-second");
        Assert.Equal(RegistrationStatus.Waitlisted, second.Status);
        Assert.Single(store.Notifier.Sent);
    }

    [Fact]
    public async Task Cancelling_promotes_oldest_eligible_waitlist_entry_in_fifo_order()
    {
        await using var store = await TestStore.CreateAsync(capacity: 1);
        var confirmed = await store.RegisterAsync(store.WorkshopA, store.Alex, "fifo-confirmed");
        var first = await store.RegisterAsync(store.WorkshopA, store.Jamie, "fifo-first");
        var second = await store.RegisterAsync(store.WorkshopA, store.Sam, "fifo-second");
        await store.CancelAsync(confirmed.Id);
        Assert.Equal(RegistrationStatus.Confirmed, (await store.GetRegistrationAsync(first.Id))!.Status);
        Assert.Equal(RegistrationStatus.Waitlisted, (await store.GetRegistrationAsync(second.Id))!.Status);
        Assert.Contains(store.Notifier.Sent, item => item.Type == "Promoted" && item.RegistrationId == first.Id);
    }

    [Fact]
    public async Task Promotion_keeps_fifo_order_when_timestamps_tie_and_ids_sort_backwards()
    {
        await using var store = await TestStore.CreateAsync(capacity: 1);
        var confirmed = await store.RegisterAsync(store.WorkshopA, store.Alex, "tie-confirmed");
        var first = await store.RegisterAsync(store.WorkshopA, store.Jamie, "tie-first");
        var second = await store.RegisterAsync(store.WorkshopA, store.Sam, "tie-second");
        var firstId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var sameCreatedAt = new DateTime(2030, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        await using (var db = store.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Registrations SET Id = {firstId}, CreatedAt = {sameCreatedAt} WHERE Id = {first.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Registrations SET Id = {secondId}, CreatedAt = {sameCreatedAt} WHERE Id = {second.Id}");
        }

        await store.CancelAsync(confirmed.Id);

        Assert.Equal(RegistrationStatus.Confirmed, (await store.GetRegistrationAsync(firstId))!.Status);
        Assert.Equal(RegistrationStatus.Waitlisted, (await store.GetRegistrationAsync(secondId))!.Status);
    }

    [Fact]
    public async Task Promotion_skips_waitlisted_participant_with_overlapping_confirmed_workshop()
    {
        await using var store = await TestStore.CreateAsync(capacity: 1);
        await store.RegisterAsync(store.WorkshopA, store.Alex, "conflicting-booking");
        var occupant = await store.RegisterAsync(store.WorkshopB, store.Sam, "target-occupant");
        var blocked = await store.RegisterAsync(store.WorkshopB, store.Alex, "target-blocked");
        var eligible = await store.RegisterAsync(store.WorkshopB, store.Jamie, "target-eligible");
        await store.CancelAsync(occupant.Id);
        Assert.Equal(RegistrationStatus.Waitlisted, (await store.GetRegistrationAsync(blocked.Id))!.Status);
        Assert.Equal(RegistrationStatus.Confirmed, (await store.GetRegistrationAsync(eligible.Id))!.Status);
    }

    [Fact]
    public async Task Rejects_overlapping_confirmed_registration_but_allows_sessions_that_touch()
    {
        await using var store = await TestStore.CreateAsync(capacity: 2);
        await store.RegisterAsync(store.WorkshopA, store.Alex, "touch-first");
        await Assert.ThrowsAsync<DomainException>(() => store.RegisterAsync(store.WorkshopB, store.Alex, "overlap"));
        var touching = await store.RegisterAsync(store.WorkshopTouching, store.Alex, "touch-second");
        Assert.Equal(RegistrationStatus.Confirmed, touching.Status);
    }

    [Fact]
    public async Task Prevents_duplicate_active_registration_and_allows_registration_after_cancel()
    {
        await using var store = await TestStore.CreateAsync(capacity: 2);
        var original = await store.RegisterAsync(store.WorkshopA, store.Alex, "duplicate-original");
        await Assert.ThrowsAsync<DomainException>(() => store.RegisterAsync(store.WorkshopA, store.Alex, "duplicate-again"));
        await store.CancelAsync(original.Id);
        var replacement = await store.RegisterAsync(store.WorkshopA, store.Alex, "duplicate-replacement");
        Assert.NotEqual(original.Id, replacement.Id);
        Assert.Equal(RegistrationStatus.Confirmed, replacement.Status);
    }

    [Fact]
    public async Task Repeating_same_idempotency_key_returns_the_same_registration()
    {
        await using var store = await TestStore.CreateAsync(capacity: 2);
        var first = await store.RegisterAsync(store.WorkshopA, store.Alex, "idempotent");
        var replay = await store.RegisterAsync(store.WorkshopA, store.Alex, "idempotent");
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, await store.RegistrationCountAsync());
        await Assert.ThrowsAsync<DomainException>(() => store.RegisterAsync(store.WorkshopA, store.Jamie, "idempotent"));
    }

    [Fact]
    public async Task Cancelling_twice_is_idempotent()
    {
        await using var store = await TestStore.CreateAsync(capacity: 2);
        var registration = await store.RegisterAsync(store.WorkshopA, store.Alex, "cancel-twice");
        var first = await store.CancelAsync(registration.Id);
        var second = await store.CancelAsync(registration.Id);
        Assert.Equal(RegistrationStatus.Cancelled, first.Status);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(store.Notifier.Sent, item => item.Type == "Cancelled");
    }

    [Fact]
    public async Task Notification_failure_does_not_undo_registration_or_cancellation()
    {
        await using var store = await TestStore.CreateAsync(capacity: 1);
        store.Notifier.Fail = true;
        var registration = await store.RegisterAsync(store.WorkshopA, store.Alex, "notification-failure");
        Assert.Equal(RegistrationStatus.Confirmed, (await store.GetRegistrationAsync(registration.Id))!.Status);
        var cancelled = await store.CancelAsync(registration.Id);
        Assert.Equal(RegistrationStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Participant_schedule_tracks_confirmation_and_cancellation()
    {
        await using var store = await TestStore.CreateAsync(capacity: 2);
        var registration = await store.RegisterAsync(store.WorkshopA, store.Alex, "schedule-refresh");
        Assert.Single(await store.GetScheduleAsync(store.Alex));
        await store.CancelAsync(registration.Id);
        Assert.Empty(await store.GetScheduleAsync(store.Alex));
    }

    [Fact]
    public async Task Independent_concurrent_callers_cannot_take_the_final_seat_twice()
    {
        await using var store = await TestStore.CreateAsync(capacity: 1);
        var outcomes = await store.RunTogetherAsync(
            () => store.RegisterIndependentAsync(store.WorkshopA, store.Alex, "race-a"),
            () => store.RegisterIndependentAsync(store.WorkshopA, store.Jamie, "race-b"));
        Assert.Single(outcomes, item => item.Status == RegistrationStatus.Confirmed);
        Assert.Single(outcomes, item => item.Status == RegistrationStatus.Waitlisted);
        Assert.Equal(1, await store.ConfirmedCountAsync(store.WorkshopA));
        Assert.Equal(2, await store.RegistrationCountAsync());
    }

    [Fact]
    public async Task Independent_concurrent_retries_with_same_key_persist_one_registration()
    {
        await using var store = await TestStore.CreateAsync(capacity: 2);
        var outcomes = await store.RunTogetherAsync(
            () => store.RegisterIndependentAsync(store.WorkshopA, store.Alex, "race-same-key"),
            () => store.RegisterIndependentAsync(store.WorkshopA, store.Alex, "race-same-key"));
        Assert.Equal(outcomes[0].Id, outcomes[1].Id);
        Assert.Equal(1, await store.RegistrationCountAsync());
        Assert.Equal(1, await store.IdempotencyCountAsync());
    }

    [Fact]
    public async Task Independent_concurrent_requests_cannot_confirm_overlapping_workshops_for_one_participant()
    {
        await using var store = await TestStore.CreateAsync(capacity: 2);
        async Task<(bool Succeeded, int Status)> TryRegister(Guid workshop, string key)
        {
            try
            {
                var result = await store.RegisterIndependentAsync(workshop, store.Alex, key);
                return (true, (int)result.Status);
            }
            catch (DomainException exception) { return (false, exception.StatusCode); }
        }
        var outcomes = await store.RunTogetherAsync(
            () => TryRegister(store.WorkshopA, "overlap-race-a"),
            () => TryRegister(store.WorkshopB, "overlap-race-b"));
        Assert.Single(outcomes, outcome => outcome.Succeeded);
        Assert.Single(outcomes, outcome => outcome.Status == 409);
        Assert.Equal(1, await store.RegistrationCountAsync());
        Assert.Equal(1, (await store.ScheduleCountAsync(store.Alex)));
    }

    private sealed class TestStore : IAsyncDisposable
    {
        private readonly string _path;
        private readonly DbContextOptions<SeatFlowDbContext> _options;
        private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
        public readonly Guid WorkshopA = Guid.NewGuid();
        public readonly Guid WorkshopB = Guid.NewGuid();
        public readonly Guid WorkshopTouching = Guid.NewGuid();
        public readonly Guid Alex = Guid.NewGuid();
        public readonly Guid Jamie = Guid.NewGuid();
        public readonly Guid Sam = Guid.NewGuid();
        public readonly TestNotifier Notifier = new();

        private TestStore()
        {
            _path = Path.Combine(Path.GetTempPath(), $"seatflow-{Guid.NewGuid():N}.db");
            _options = new DbContextOptionsBuilder<SeatFlowDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = _path, DefaultTimeout = 30, ForeignKeys = true }.ToString())
                .Options;
        }

        public static async Task<TestStore> CreateAsync(int capacity)
        {
            var store = new TestStore();
            await using var db = store.CreateContext();
            await db.Database.EnsureCreatedAsync();
            var baseTime = DateTime.SpecifyKind(new DateTime(2030, 1, 1, 9, 0, 0), DateTimeKind.Utc);
            db.Workshops.AddRange(
                new Workshop { Id = store.WorkshopA, Title = "Workshop A", StartTime = baseTime, EndTime = baseTime.AddHours(1), Capacity = capacity },
                new Workshop { Id = store.WorkshopB, Title = "Workshop B", StartTime = baseTime.AddMinutes(30), EndTime = baseTime.AddHours(1.5), Capacity = capacity },
                new Workshop { Id = store.WorkshopTouching, Title = "Touching session", StartTime = baseTime.AddHours(1), EndTime = baseTime.AddHours(2), Capacity = capacity });
            db.Participants.AddRange(
                new Participant { Id = store.Alex, Name = "Alex", Email = "alex@test.local" },
                new Participant { Id = store.Jamie, Name = "Jamie", Email = "jamie@test.local" },
                new Participant { Id = store.Sam, Name = "Sam", Email = "sam@test.local" });
            await db.SaveChangesAsync();
            return store;
        }

        public SeatFlowDbContext CreateContext() => new(_options);
        private RegistrationService CreateService(SeatFlowDbContext db) => new(db, Notifier, _loggerFactory.CreateLogger<RegistrationService>());
        public async Task<RegistrationView> RegisterAsync(Guid workshop, Guid participant, string key)
        {
            await using var db = CreateContext();
            return await CreateService(db).RegisterAsync(workshop, participant, key, CancellationToken.None);
        }
        public async Task<RegistrationView> RegisterIndependentAsync(Guid workshop, Guid participant, string key) => await RegisterAsync(workshop, participant, key);
        public async Task<RegistrationView> CancelAsync(Guid registration)
        {
            await using var db = CreateContext();
            return await CreateService(db).CancelAsync(registration, CancellationToken.None);
        }
        public async Task<RegistrationView?> GetRegistrationAsync(Guid registration)
        {
            await using var db = CreateContext();
            return await CreateService(db).GetRegistrationAsync(registration, CancellationToken.None);
        }
        public async Task<int> RegistrationCountAsync()
        {
            await using var db = CreateContext();
            return await db.Registrations.CountAsync();
        }
        public async Task<int> ConfirmedCountAsync(Guid workshop)
        {
            await using var db = CreateContext();
            return await db.Registrations.CountAsync(x => x.WorkshopId == workshop && x.Status == RegistrationStatus.Confirmed);
        }
        public async Task<int> IdempotencyCountAsync()
        {
            await using var db = CreateContext();
            return await db.IdempotencyRecords.CountAsync();
        }
        public async Task<int> ScheduleCountAsync(Guid participant)
        {
            await using var db = CreateContext();
            return await db.Registrations.CountAsync(x => x.ParticipantId == participant && x.Status == RegistrationStatus.Confirmed);
        }
        public async Task<IReadOnlyList<ScheduleItem>> GetScheduleAsync(Guid participant)
        {
            await using var db = CreateContext();
            return await CreateService(db).GetScheduleAsync(participant, CancellationToken.None);
        }
        public async Task<T[]> RunTogetherAsync<T>(Func<Task<T>> first, Func<Task<T>> second)
        {
            var arrivals = 0;
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<T> Start(Func<Task<T>> operation)
            {
                if (Interlocked.Increment(ref arrivals) == 2) release.TrySetResult();
                await release.Task;
                return await operation();
            }
            return await Task.WhenAll(Task.Run(() => Start(first)), Task.Run(() => Start(second)));
        }
        public ValueTask DisposeAsync()
        {
            _loggerFactory.Dispose();
            foreach (var file in new[] { _path, _path + "-shm", _path + "-wal" })
                if (File.Exists(file)) File.Delete(file);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestNotifier : INotificationService
    {
        public List<SeatFlowNotification> Sent { get; } = [];
        public bool Fail { get; set; }
        public Task SendAsync(SeatFlowNotification notification, CancellationToken cancellationToken)
        {
            if (Fail) throw new InvalidOperationException("simulated failure");
            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }
}
