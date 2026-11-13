using OrderFlow.Application.Abstractions;

namespace OrderFlow.Adapters.Outbound.Time;

/// <summary>
/// <see cref="IClock"/> adapter backed by the system clock, always in UTC.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
