using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeatFlow.Api;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

public sealed class RecordingNotifications : INotificationService
{
    private readonly List<Notification> _sent = [];

    public IReadOnlyList<Notification> Sent
    {
        get
        {
            lock (_sent) return _sent.ToList();
        }
    }

    public Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        lock (_sent) _sent.Add(notification);
        return Task.CompletedTask;
    }
}

public sealed class FailingNotifications : INotificationService
{
    public Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Notification gateway is unavailable.");
}

/// <summary>Builds the real service graph against a fresh, empty SQLite file for each test.</summary>
public sealed class SeatFlowHarness : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provider;

    public RecordingNotifications Recorder { get; } = new();

    public SeatFlowHarness(INotificationService? notifications = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "seatflow-tests");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, $"{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSeatFlow($"Data Source={_dbPath}");
        services.Replace(ServiceDescriptor.Singleton(notifications ?? Recorder));
        _provider = services.BuildServiceProvider();

        using var db = _provider.GetRequiredService<IDbContextFactory<SeatFlowDbContext>>().CreateDbContext();
        db.Database.EnsureCreated();
    }

    public RegistrationService Registrations => _provider.GetRequiredService<RegistrationService>();
    public CatalogService Catalog => _provider.GetRequiredService<CatalogService>();

    public async Task<int> AddParticipantAsync(string name)
    {
        await using var db = await DbFactory().CreateDbContextAsync();
        var participant = new Participant { Name = name, Email = $"{Guid.NewGuid():N}@test.local" };
        db.Participants.Add(participant);
        await db.SaveChangesAsync();
        return participant.Id;
    }

    /// <summary>Creates a workshop through the catalog service. Defaults to a two-hour slot on 1 Dec 2026, 10:00 UTC.</summary>
    public async Task<int> AddWorkshopAsync(int capacity, DateTimeOffset? startsAt = null, int hours = 2)
    {
        var start = startsAt ?? new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero);
        var workshop = await Catalog.CreateWorkshopAsync("Test workshop", start, start.AddHours(hours), capacity);
        return workshop.Id;
    }

    public async Task<RegistrationStatus> StatusOfAsync(int registrationId)
    {
        await using var db = await DbFactory().CreateDbContextAsync();
        return (await db.Registrations.FindAsync(registrationId))!.Status;
    }

    public async Task<int> CountRegistrationsAsync(int workshopId, RegistrationStatus status)
    {
        await using var db = await DbFactory().CreateDbContextAsync();
        return await db.Registrations.CountAsync(r => r.WorkshopId == workshopId && r.Status == status);
    }

    private IDbContextFactory<SeatFlowDbContext> DbFactory() =>
        _provider.GetRequiredService<IDbContextFactory<SeatFlowDbContext>>();

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Temp file cleanup is best-effort.
        }
    }
}
