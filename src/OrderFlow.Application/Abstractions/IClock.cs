namespace OrderFlow.Application.Abstractions;

/// <summary>
/// Outbound port that supplies the current time.
/// </summary>
/// <remarks>
/// Injecting time keeps use cases deterministic and testable (a fixed clock in
/// tests) and avoids static <c>DateTime.UtcNow</c> calls inside the domain.
/// </remarks>
public interface IClock
{
    /// <summary>Gets the current UTC instant.</summary>
    DateTime UtcNow { get; }
}
