namespace OrderFlow.Adapters.Outbound.Payments.Asaas;

/// <summary>
/// Configuration of the Asaas adapter.
/// </summary>
/// <remarks>
/// Bound from the <c>Payments:Asaas</c> configuration section.
/// </remarks>
public sealed class AsaasOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Payments:Asaas";

    /// <summary>Gets or sets the access key (<c>$sandbox_access_token_…</c> for the sandbox).</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the API base address.</summary>
    public string BaseUrl { get; set; } = "https://api.asaas.com/v3";

    /// <summary>Gets or sets the number of days until the payment is due.</summary>
    public int DueDays { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether a customer is created on demand
    /// when the charge does not carry one.
    /// </summary>
    public bool AutoCreateCustomer { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the adapter is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets a value indicating whether the adapter can be used.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(AccessKey);
}
