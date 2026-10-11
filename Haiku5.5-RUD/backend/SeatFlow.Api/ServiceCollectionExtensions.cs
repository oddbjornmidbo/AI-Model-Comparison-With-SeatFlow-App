using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Notifications;
using SeatFlow.Api.Services;

namespace SeatFlow.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSeatFlow(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<SeatFlowDbContext>(o => o.UseSqlite(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<INotificationService, InMemoryNotificationService>();
        services.AddSingleton<RegistrationService>();
        services.AddSingleton<CatalogService>();
        services.AddProblemDetails();
        services.AddExceptionHandler<SeatFlowExceptionHandler>();
        return services;
    }
}
