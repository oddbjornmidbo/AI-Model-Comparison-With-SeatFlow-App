using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SeatFlow.Core;

public enum NotificationKind
{
    RegistrationConfirmed,
    WaitlistPromoted,
    RegistrationCancelled
}

public sealed record Notification(
    NotificationKind Kind,
    int RegistrationId,
    int ParticipantId,
    int WorkshopId,
    string Message,
    DateTime CreatedAt);

/// <summary>Integration boundary for outgoing notifications (email, SMS, ...).</summary>
public interface INotificationService
{
    Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default);
}

/// <summary>Development implementation: keeps notifications in memory and logs them.</summary>
public sealed class InMemoryNotificationService(ILogger<InMemoryNotificationService> logger) : INotificationService
{
    private readonly ConcurrentQueue<Notification> _sent = new();

    public IReadOnlyCollection<Notification> Sent => _sent.ToArray();

    public Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(notification);
        logger.LogInformation("Notification {Kind} for registration {RegistrationId}: {Message}",
            notification.Kind, notification.RegistrationId, notification.Message);
        return Task.CompletedTask;
    }
}
