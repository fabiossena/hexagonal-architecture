namespace OrderFlow.Adapters.Outbound.Payments;

/// <summary>
/// Configuration of the Stripe adapter.
/// </summary>
/// <remarks>
/// Bound from the <c>Payments:Stripe</c> configuration section. The API key is
/// never exposed through the API — only <c>IsConfigured</c> is.
/// </remarks>
public sealed class StripeOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Payments:Stripe";

    /// <summary>Gets or sets the secret API key (sk_test_… / sk_live_…).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the API base address.</summary>
    public string BaseUrl { get; set; } = "https://api.stripe.com";

    /// <summary>Gets or sets the currency used for charges (lower case, e.g. <c>brl</c>).</summary>
    public string Currency { get; set; } = "brl";

    /// <summary>Gets or sets the URL Stripe redirects to after a successful checkout.</summary>
    public string SuccessUrl { get; set; } = "http://localhost:5080/payments/success";

    /// <summary>Gets or sets the URL Stripe redirects to when the customer abandons the checkout.</summary>
    public string CancelUrl { get; set; } = "http://localhost:5080/payments/cancel";

    /// <summary>
    /// Gets or sets the test payment method used for card charges. Stripe's test
    /// mode accepts <c>pm_card_visa</c>, <c>pm_card_mastercard</c>, …
    /// </summary>
    public string CardPaymentMethod { get; set; } = "pm_card_visa";

    /// <summary>Gets or sets a value indicating whether the adapter is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets a value indicating whether the adapter can be used.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
