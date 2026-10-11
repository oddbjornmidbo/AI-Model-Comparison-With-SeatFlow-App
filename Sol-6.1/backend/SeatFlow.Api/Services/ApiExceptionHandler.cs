using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace SeatFlow.Api.Services;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ApiException error => (error.StatusCode, error.Code, error.Message),
            SqliteException { SqliteErrorCode: 5 or 6 } =>
                (503, "DatabaseBusy", "The database is busy. Please retry the request using the same idempotency key."),
            _ => (500, "ServerError", "An unexpected error occurred.")
        };
        if (status >= 500)
            logger.LogError(exception, "Request failed with status {StatusCode}", status);
        httpContext.Response.StatusCode = status;
        if (status == 503)
            httpContext.Response.Headers.RetryAfter = "1";
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = title, Detail = detail, Instance = httpContext.Request.Path
        }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
