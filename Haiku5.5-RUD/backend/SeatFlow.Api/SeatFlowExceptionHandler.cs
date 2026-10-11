using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SeatFlow.Api.Services;

namespace SeatFlow.Api;

/// <summary>Maps domain failures to ProblemDetails responses with the matching HTTP status.</summary>
public sealed class SeatFlowExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not SeatFlowException sf)
        {
            return false;
        }

        var (status, title) = sf.Kind switch
        {
            SeatFlowErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            SeatFlowErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status400BadRequest, "Validation failed")
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = sf.Message
        }, cancellationToken);
        return true;
    }
}
