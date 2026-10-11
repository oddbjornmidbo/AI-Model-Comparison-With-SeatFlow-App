using System.Collections.Concurrent;

namespace SeatFlow.Api.Notifications;

/// <summary>
/// Development implementation: logs each notification and keeps it in memory
/// so it can be inspected (for example by tests or the dev-only endpoint).
/// </summary>
public sealed class InMemoryNotificationService(ILogger<InMemoryNotificationService> logger) : INotificationService
{
    private readonly ConcurrentQueue<Notification> _sent = new();

    public IReadOnlyCollection<Notification> Sent => _sent.ToArray();

    public Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(notification);
        logger.LogInformation("Notification {Kind} for participant {ParticipantId}: {Message}",
            notification.Kind, notification.ParticipantId, notification.Message);
        return Task.CompletedTask;
    }
}
