namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Body of <c>POST /api/checkout/payments</c>.
/// </summary>
/// <param name="QuoteId">Identifier of the quote being paid (the OrderFlow order reference).</param>
/// <param name="PaymentMethodCode">iAppForms method code (<c>Pix</c>, <c>CreditCard</c>, <c>DebitCard</c>, <c>Boleto</c>).</param>
/// <param name="Recurring">Whether the payment is part of a subscription.</param>
/// <param name="DomainId">Optional iAppForms domain, kept for traceability.</param>
public sealed record PaymentSurfaceRequest(
    Guid QuoteId,
    string? PaymentMethodCode,
    bool Recurring = false,
    Guid? DomainId = null);

/// <summary>
/// Answer of <c>POST /api/checkout/payments</c>.
/// </summary>
/// <param name="PaymentId">OrderFlow payment identity.</param>
/// <param name="Status">Commercial status (<c>InReview</c>, <c>Approved</c>, <c>Rejected</c>, <c>Error</c>, <c>Cancelled</c>).</param>
/// <param name="ProviderStatus">Settlement status reported by the provider.</param>
/// <param name="Provider">Adapter name that processed the charge.</param>
/// <param name="CheckoutUrl">Hosted checkout URL, when the provider returns one.</param>
/// <param name="Instructions">How the customer completes the payment (Pix, boleto, …).</param>
/// <param name="Reused"><c>true</c> when the idempotency key replayed an existing payment.</param>
public sealed record PaymentSurfaceDto(
    Guid PaymentId,
    string Status,
    string? ProviderStatus,
    string? Provider,
    string? CheckoutUrl,
    ProviderInstructions? Instructions,
    bool Reused);

/// <summary>
/// Answer of <c>GET /api/checkout/payments/{id}</c>.
/// </summary>
/// <param name="PaymentId">OrderFlow payment identity.</param>
/// <param name="Status">Commercial status.</param>
/// <param name="ProviderStatus">Settlement status reported by the provider.</param>
/// <param name="Provider">Adapter name that processed the charge.</param>
/// <param name="MethodCode">iAppForms method code.</param>
/// <param name="Amount">Amount charged.</param>
/// <param name="Currency">ISO-4217 currency.</param>
/// <param name="CheckoutUrl">Hosted checkout URL, when the provider returns one.</param>
/// <param name="Instructions">How the customer completes the payment.</param>
/// <param name="SupportedOperations">Operations still available on this payment.</param>
/// <param name="RequestedAtUtc">
/// Instant the payment was requested (UTC). Extra field: iAppForms counts its
/// 72h confirmation window from this moment.
/// </param>
public sealed record PaymentStatusDto(
    Guid PaymentId,
    string Status,
    string? ProviderStatus,
    string? Provider,
    string? MethodCode,
    decimal Amount,
    string? Currency,
    string? CheckoutUrl,
    ProviderInstructions? Instructions,
    IReadOnlyList<string> SupportedOperations,
    DateTime? RequestedAtUtc = null);

/// <summary>
/// How the customer completes the payment.
/// </summary>
/// <param name="MethodCode">iAppForms method code.</param>
/// <param name="QrCodeImage">QR code image (base64), when the provider returns one.</param>
/// <param name="CopyPaste">Pix copy-and-paste code.</param>
/// <param name="Barcode">Barcode, when the provider returns one.</param>
/// <param name="Url">Hosted checkout URL.</param>
/// <param name="Fields">Extra provider fields.</param>
public sealed record ProviderInstructions(
    string MethodCode,
    string? QrCodeImage = null,
    string? CopyPaste = null,
    string? Barcode = null,
    string? Url = null,
    IReadOnlyDictionary<string, string>? Fields = null);

/// <summary>
/// Answer of <c>POST /api/checkout/webhooks/{provider}</c>.
/// </summary>
/// <param name="outcome">What happened to the notification (<c>Applied</c>, <c>UnknownPayment</c>, <c>Skipped</c>).</param>
/// <param name="providerPaymentId">Provider reference, when it could be read.</param>
public sealed record WebhookSurfaceDto(string Outcome, string? ProviderPaymentId = null);
