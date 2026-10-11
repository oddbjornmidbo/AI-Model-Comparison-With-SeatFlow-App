namespace SeatFlow.Api.Services;

public enum NotificationKind { Confirmed, Promoted, Cancelled }

public sealed record RegistrationNotification(long RegistrationId, long ParticipantId, long WorkshopId,
    NotificationKind Kind);

public interface INotificationService
{
    Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default);
}

public sealed class LoggingNotificationService(ILogger<LoggingNotificationService> logger) : INotificationService
{
    public Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Registration {RegistrationId}: {NotificationKind}; workshop {WorkshopId}, participant {ParticipantId}",
            notification.RegistrationId, notification.Kind, notification.WorkshopId, notification.ParticipantId);
        return Task.CompletedTask;
    }
}
