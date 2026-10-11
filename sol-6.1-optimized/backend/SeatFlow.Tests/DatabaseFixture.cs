using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

/// <summary>
/// Every test gets a real SQLite file. Every operation creates a new DbContext and
/// connection, so the tests cannot pass because a shared tracker serializes writes.
/// </summary>
internal sealed class DatabaseFixture : IAsyncDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "seatflow-tests", Guid.NewGuid().ToString("N"));
    public string ConnectionString { get; }
    public RecordingNotifications Notifications { get; } = new();
    public TimeProvider Clock { get; set; } = new FixedClock();

    private DatabaseFixture()
    {
        Directory.CreateDirectory(directory);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "registrations.sqlite"),
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 30
        }.ToString();
    }

    public static async Task<DatabaseFixture> CreateAsync()
    {
        var fixture = new DatabaseFixture();
        await using var db = fixture.NewContext();
        await DatabaseInitializer.InitializeAsync(db, seed: false);
        return fixture;
    }

    public SeatFlowDbContext NewContext() => new(new DbContextOptionsBuilder<SeatFlowDbContext>()
        .UseSqlite(ConnectionString).Options);

    public RegistrationService Service(SeatFlowDbContext db, INotificationService? notifications = null) =>
        new(db, notifications ?? Notifications, Clock, NullLogger<RegistrationService>.Instance);

    public async Task<Workshop> WorkshopAsync(int capacity = 1, int startHour = 10, int endHour = 11, string title = "Practical design")
    {
        await using var db = NewContext();
        var workshop = new Workshop
        {
            Title = title, Capacity = capacity,
            Start = new DateTime(2030, 6, 12, startHour, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2030, 6, 12, endHour, 0, 0, DateTimeKind.Utc)
        };
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();
        return workshop;
    }

    public async Task<Participant> ParticipantAsync(string name = "Alex Morgan")
    {
        await using var db = NewContext();
        var participant = new Participant { Name = name, Email = $"{Guid.NewGuid():N}@example.test" };
        db.Participants.Add(participant);
        await db.SaveChangesAsync();
        return participant;
    }

    public async Task<RegistrationResult> RegisterAsync(Workshop workshop, Participant participant, string? key = null, INotificationService? notifications = null)
    {
        await using var db = NewContext();
        return await Service(db, notifications).RegisterAsync(workshop.Id, participant.Id, key ?? Guid.NewGuid().ToString("N"));
    }

    public async Task<CancellationResult> CancelAsync(long registrationId, INotificationService? notifications = null)
    {
        await using var db = NewContext();
        return await Service(db, notifications).CancelAsync(registrationId);
    }

    public async Task<Registration[]> RegistrationsAsync()
    {
        await using var db = NewContext();
        return await db.Registrations.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
    }

    /// <summary>
    /// Dedicated worker threads open their independent connections before meeting
    /// at the barrier. All writers are released together, before any transaction starts.
    /// </summary>
    public async Task<T[]> RaceAsync<T>(params Func<SeatFlowDbContext, Task<T>>[] operations)
    {
        using var barrier = new Barrier(operations.Length + 1);
        var connections = new ConcurrentBag<SqliteConnection>();
        var tasks = operations.Select(operation => Task.Factory.StartNew(() =>
        {
            using var db = NewContext();
            db.Database.OpenConnection();
            connections.Add((SqliteConnection)db.Database.GetDbConnection());
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("Writers did not all reach the contention gate.");
            return operation(db).GetAwaiter().GetResult();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20)))
            throw new TimeoutException("Writers did not all reach the contention gate.");
        Assert.Equal(operations.Length, connections.Distinct(ReferenceEqualityComparer.Instance).Count());
        return await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(60));
    }

    public ValueTask DisposeAsync()
    {
        Directory.Delete(directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}

internal sealed class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2030, 6, 12, 8, 0, 0, TimeSpan.Zero);
}

internal sealed class RecordingNotifications : INotificationService
{
    public ConcurrentQueue<RegistrationNotification> Sent { get; } = new();
    public bool Throw { get; set; }
    public Func<RegistrationNotification, Task>? Observer { get; set; }

    public async Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(notification);
        if (Observer is not null) await Observer(notification);
        if (Throw) throw new InvalidOperationException("Notification provider is unavailable.");
    }
}
