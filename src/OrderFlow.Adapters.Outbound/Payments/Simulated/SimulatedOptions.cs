namespace OrderFlow.Adapters.Outbound.Payments.Simulated;

/// <summary>
/// Configuration of the built-in simulator.
/// </summary>
/// <remarks>
/// Bound from the <c>Payments:Simulated</c> configuration section.
/// </remarks>
public sealed class SimulatedOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Payments:Simulated";

    /// <summary>
    /// Gets or sets how long a pending simulated payment takes to be confirmed.
    /// It makes the "refresh status" button of the test screen show a real
    /// transition from <c>Pending</c> to <c>Paid</c>.
    /// </summary>
    public int AutoPayAfterSeconds { get; set; } = 15;

    /// <summary>Gets or sets a value indicating whether the simulator is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets a value indicating whether the simulator can be used.</summary>
    public bool IsConfigured => Enabled;
}
