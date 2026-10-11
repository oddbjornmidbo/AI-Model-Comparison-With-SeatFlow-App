namespace SeatFlow.Api.Services;

public enum SeatFlowErrorKind
{
    NotFound,
    Conflict,
    Validation
}

/// <summary>A business-rule or lookup failure that the API maps to an HTTP status.</summary>
public sealed class SeatFlowException(SeatFlowErrorKind kind, string message) : Exception(message)
{
    public SeatFlowErrorKind Kind { get; } = kind;
}
