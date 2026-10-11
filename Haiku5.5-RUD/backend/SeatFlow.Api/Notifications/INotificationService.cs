namespace SeatFlow.Api.Notifications;

public enum NotificationKind
{
    RegistrationConfirmed,
    WaitlistPromoted,
    RegistrationCancelled
}

public sealed record Notification(
    NotificationKind Kind,
    int ParticipantId,
    int WorkshopId,
    int RegistrationId,
    string Message);

/// <summary>
/// Outbound notification boundary. Implementations must not be relied on for
/// transactional correctness: callers swallow and log failures.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default);
}
