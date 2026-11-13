namespace OrderFlow.Domain.Payments;

/// <summary>
/// Payment providers supported by the service.
/// </summary>
/// <remarks>
/// The enum only carries *identifiers*: every vendor-specific detail (endpoints,
/// payloads, authentication) lives in the outbound adapters, so the domain and the
/// application layers never depend on a payment SDK.
/// </remarks>
public enum PaymentProvider
{
    /// <summary>Stripe — credit/debit card, Pix and Boleto.</summary>
    Stripe = 0,

    /// <summary>Mercado Pago — credit/debit card, Pix, Boleto and bank transfer.</summary>
    MercadoPago = 1,

    /// <summary>Asaas — credit/debit card, Pix and Boleto.</summary>
    Asaas = 2,

    /// <summary>
    /// Built-in simulator. It answers like a real provider would but never calls
    /// an external API, so the whole flow can be exercised without credentials.
    /// </summary>
    Simulated = 3
}
