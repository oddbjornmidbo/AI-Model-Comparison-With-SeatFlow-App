using System.Collections.Concurrent;

namespace SeatFlow.Api;

public sealed record SeatFlowNotification(string Type, Guid RegistrationId, Guid WorkshopId, Guid ParticipantId, DateTime OccurredAt);

public interface INotificationService
{
    Task SendAsync(SeatFlowNotification notification, CancellationToken cancellationToken);
}

public sealed class DevelopmentNotificationService : INotificationService
{
    private readonly ConcurrentQueue<SeatFlowNotification> _notifications = new();

    public IReadOnlyCollection<SeatFlowNotification> Notifications => _notifications.ToArray();

    public Task SendAsync(SeatFlowNotification notification, CancellationToken cancellationToken)
    {
        _notifications.Enqueue(notification);
        return Task.CompletedTask;
    }
}
