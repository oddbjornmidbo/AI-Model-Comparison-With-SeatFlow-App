namespace SeatFlow.Core;

/// <summary>Base for errors the API maps to client-facing status codes.</summary>
public abstract class SeatFlowException(string message) : Exception(message);

public sealed class NotFoundException(string message) : SeatFlowException(message);

/// <summary>A business rule was violated (duplicate registration, schedule conflict, ...).</summary>
public sealed class ConflictException(string message) : SeatFlowException(message);

public sealed class ValidationFailedException(IDictionary<string, string[]> errors)
    : SeatFlowException("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
