using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeatFlow.Core;

namespace SeatFlow.Tests;

/// <summary>
/// A throwaway SQLite file database with a real SeatFlowService on top. A file (not in-memory) is used so
/// that concurrent contexts behave like production.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"seatflow-tests-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<SeatFlowDbContext> _factory;

    public TestDatabase(INotificationService? notifications = null)
    {
        var options = new DbContextOptionsBuilder<SeatFlowDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;
        _factory = new ContextFactory(options);

        using (var db = _factory.CreateDbContext())
            db.Database.EnsureCreated();

        Notifications = new InMemoryNotificationService(NullLogger<InMemoryNotificationService>.Instance);
        Service = new SeatFlowService(_factory, notifications ?? Notifications, NullLogger<SeatFlowService>.Instance);
    }

    public SeatFlowService Service { get; }

    public InMemoryNotificationService Notifications { get; }

    /// <summary>Times are expressed as hours on a fixed UTC day.</summary>
    public static DateTime At(double hour) => new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc).AddHours(hour);

    public Task<Workshop> AddWorkshopAsync(string title, double startHour, double endHour, int capacity) =>
        Service.CreateWorkshopAsync(title, At(startHour), At(endHour), capacity);

    public async Task<Participant> AddParticipantAsync(string name)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var participant = new Participant
        {
            Name = name,
            Email = $"{name.ToLowerInvariant().Replace(' ', '.')}@example.com"
        };
        db.Participants.Add(participant);
        await db.SaveChangesAsync();
        return participant;
    }

    public async Task<Registration> GetRegistrationAsync(int registrationId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Registrations
            .Include(r => r.Workshop)
            .Include(r => r.Participant)
            .SingleAsync(r => r.Id == registrationId);
    }

    public async Task<int> CountRegistrationsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Registrations.CountAsync();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private sealed class ContextFactory(DbContextOptions<SeatFlowDbContext> options) : IDbContextFactory<SeatFlowDbContext>
    {
        public SeatFlowDbContext CreateDbContext() => new(options);

        public Task<SeatFlowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

/// <summary>Notification sink that always fails, used to prove failures do not roll back business changes.</summary>
public sealed class FailingNotificationService : INotificationService
{
    public Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Notification gateway is down");
}
