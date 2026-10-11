using System.Collections.Concurrent;

namespace SeatFlow.Api.Services;

public enum NotificationKind { Confirmed, Promoted, Cancelled }
public sealed record RegistrationNotification(int RegistrationId, int WorkshopId,
    int ParticipantId, NotificationKind Kind, DateTime OccurredAt);

public interface INotificationService
{
    Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default);
}

public sealed class DevelopmentNotificationService(ILogger<DevelopmentNotificationService> logger)
    : INotificationService
{
    private readonly ConcurrentQueue<RegistrationNotification> notifications = new();
    public IReadOnlyCollection<RegistrationNotification> Notifications => notifications.ToArray();

    public Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default)
    {
        notifications.Enqueue(notification);
        logger.LogInformation("{Kind}: registration {RegistrationId}, workshop {WorkshopId}, participant {ParticipantId}",
            notification.Kind, notification.RegistrationId, notification.WorkshopId, notification.ParticipantId);
        return Task.CompletedTask;
    }
}
