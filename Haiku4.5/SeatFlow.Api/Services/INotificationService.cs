using SeatFlow.Api.Models;

namespace SeatFlow.Api.Services;

public interface INotificationService
{
    Task SendRegistrationConfirmedAsync(Registration registration);
    Task SendWaitlistPromotedAsync(Registration registration);
    Task SendRegistrationCancelledAsync(Registration registration);
}
