namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Configuration of the iAppForms integration.
/// </summary>
/// <remarks>
/// Bound from the <c>IAppForms</c> configuration section.
/// </remarks>
public sealed class IAppFormsOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "IAppForms";

    /// <summary>
    /// Gets or sets a value indicating whether the compatibility surface is
    /// exposed at all.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the shared secret expected in the <c>X-Api-Key</c> header.
    /// </summary>
    /// <remarks>
    /// When it is empty the surface is open (development). Set it before
    /// pointing the iAppForms production traffic here.
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the provider used when none of the preferred ones is
    /// configured (optional).
    /// </summary>
    public string? DefaultProvider { get; set; }

    /// <summary>
    /// Gets or sets the provider preference per payment method.
    /// </summary>
    /// <remarks>
    /// The first provider of the list that is registered, configured and able to
    /// handle the method wins. The simulator is the last resort, so the surface
    /// always works — even without any credential.
    /// </remarks>
    public Dictionary<string, string[]> ProviderPreferenceByMethod { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pix"] = new[] { "MercadoPago", "Asaas", "Stripe", "Simulated" },
        ["CreditCard"] = new[] { "Stripe", "Asaas", "MercadoPago", "Simulated" },
        ["DebitCard"] = new[] { "Stripe", "Asaas", "MercadoPago", "Simulated" },
        ["Boleto"] = new[] { "Asaas", "MercadoPago", "Simulated" },
        ["BankTransfer"] = new[] { "MercadoPago", "Simulated" }
    };
}
