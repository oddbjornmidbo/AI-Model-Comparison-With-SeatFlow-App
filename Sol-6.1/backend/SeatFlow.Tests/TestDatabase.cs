using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeatFlow.Api.Data;
using SeatFlow.Api.Models;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"seatflow-tests-{Guid.NewGuid():N}.db");

    public SeatFlowDbContext CreateContext() => new(new DbContextOptionsBuilder<SeatFlowDbContext>()
        .UseSqlite($"Data Source={_path};Default Timeout=30;Pooling=False")
        .EnableDetailedErrors()
        .Options);

    public RegistrationService CreateService(SeatFlowDbContext context, INotificationService? notifications = null) =>
        new(context, notifications ?? new RecordingNotifications(), NullLogger<RegistrationService>.Instance);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task<Workshop> AddWorkshopAsync(int capacity = 2, int startHour = 9, int startMinute = 0,
        int endHour = 10, int endMinute = 0)
    {
        await using var context = CreateContext();
        var workshop = new Workshop
        {
            Title = $"Workshop {Guid.NewGuid():N}",
            Capacity = capacity,
            StartTime = new DateTime(2030, 1, 15, startHour, startMinute, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2030, 1, 15, endHour, endMinute, 0, DateTimeKind.Utc)
        };
        context.Workshops.Add(workshop);
        await context.SaveChangesAsync();
        return workshop;
    }

    public async Task<Participant> AddParticipantAsync(string name = "Test participant")
    {
        await using var context = CreateContext();
        var participant = new Participant { Name = name, Email = $"{Guid.NewGuid():N}@example.test" };
        context.Participants.Add(participant);
        await context.SaveChangesAsync();
        return participant;
    }

    public async ValueTask DisposeAsync()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}

internal sealed class RecordingNotifications : INotificationService
{
    public List<RegistrationNotification> Sent { get; } = [];

    public Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default)
    {
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

internal sealed class FailingNotifications : INotificationService
{
    public Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Simulated notification outage");
}
