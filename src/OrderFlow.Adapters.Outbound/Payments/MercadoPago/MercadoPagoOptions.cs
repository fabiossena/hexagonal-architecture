namespace OrderFlow.Adapters.Outbound.Payments.MercadoPago;

/// <summary>
/// Configuration of the Mercado Pago adapter.
/// </summary>
/// <remarks>
/// Bound from the <c>Payments:MercadoPago</c> configuration section.
/// </remarks>
public sealed class MercadoPagoOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Payments:MercadoPago";

    /// <summary>Gets or sets the access token (APP_USR-… for the test account).</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the API base address.</summary>
    public string BaseUrl { get; set; } = "https://api.mercadopago.com";

    /// <summary>Gets or sets the URL Mercado Pago calls back with payment updates.</summary>
    public string NotificationUrl { get; set; } = "http://localhost:5080/api/webhooks/mercadopago";

    /// <summary>Gets or sets the statement descriptor shown on the customer's statement.</summary>
    public string StatementDescriptor { get; set; } = "ORDERFLOW";

    /// <summary>Gets or sets a value indicating whether the adapter is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets a value indicating whether the adapter can be used.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(AccessToken);
}
