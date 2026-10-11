using Microsoft.Extensions.Logging;
using SeatFlow.Api.Models;

namespace SeatFlow.Api.Services;

public class InMemoryNotificationService : INotificationService
{
    private readonly ILogger<InMemoryNotificationService> _logger;
    public List<string> NotificationLog { get; } = new();

    public InMemoryNotificationService(ILogger<InMemoryNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendRegistrationConfirmedAsync(Registration registration)
    {
        var message = $"Registration {registration.Id} confirmed for participant {registration.ParticipantId} in workshop {registration.WorkshopId}";
        _logger.LogInformation(message);
        NotificationLog.Add(message);
        return Task.CompletedTask;
    }

    public Task SendWaitlistPromotedAsync(Registration registration)
    {
        var message = $"Registration {registration.Id} promoted from waitlist for participant {registration.ParticipantId} in workshop {registration.WorkshopId}";
        _logger.LogInformation(message);
        NotificationLog.Add(message);
        return Task.CompletedTask;
    }

    public Task SendRegistrationCancelledAsync(Registration registration)
    {
        var message = $"Registration {registration.Id} cancelled for participant {registration.ParticipantId} in workshop {registration.WorkshopId}";
        _logger.LogInformation(message);
        NotificationLog.Add(message);
        return Task.CompletedTask;
    }
}
