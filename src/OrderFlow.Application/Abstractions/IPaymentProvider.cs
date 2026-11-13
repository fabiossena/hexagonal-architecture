using OrderFlow.Domain.Payments;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Abstractions;

/// <summary>
/// Outbound port implemented by every payment provider adapter
/// (Stripe, Mercado Pago, Asaas and the built-in simulator).
/// </summary>
/// <remarks>
/// The application layer talks to this contract only, so adding a provider is a
/// matter of writing an adapter — no use case changes.
/// </remarks>
public interface IPaymentProvider
{
    /// <summary>Gets the provider identity.</summary>
    PaymentProvider Provider { get; }

    /// <summary>Gets the human-readable provider name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>Gets a value indicating whether the provider is the offline simulator.</summary>
    bool IsSimulation { get; }

    /// <summary>Gets a value indicating whether the provider has the credentials it needs.</summary>
    bool IsConfigured { get; }

    /// <summary>Gets the payment instruments accepted by the provider.</summary>
    IReadOnlyCollection<PaymentMethod> SupportedMethods { get; }

    /// <summary>Charges the customer.</summary>
    /// <param name="request">Charge request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider outcome, already mapped to the domain status.</returns>
    Task<ProviderChargeResult> ChargeAsync(ProviderChargeRequest request, CancellationToken cancellationToken);

    /// <summary>Reads the current state of a charge.</summary>
    /// <param name="providerPaymentId">Provider reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider outcome, already mapped to the domain status.</returns>
    Task<ProviderChargeResult> GetChargeAsync(string providerPaymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Translates a raw provider status into the domain vocabulary.
    /// </summary>
    /// <param name="providerStatus">Status as reported by the provider.</param>
    /// <returns>The matching domain status.</returns>
    /// <remarks>
    /// Each provider owns its own status vocabulary, so the mapping lives in the
    /// adapter and the inbound webhook handler never has to know the vendor.
    /// </remarks>
    PaymentStatus MapStatus(string providerStatus);

    /// <summary>Refunds a captured charge.</summary>
    /// <param name="providerPaymentId">Provider reference.</param>
    /// <param name="amount">Amount to refund.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refund outcome.</returns>
    Task<ProviderRefundResult> RefundAsync(
        string providerPaymentId,
        Money amount,
        CancellationToken cancellationToken);
}

/// <summary>Request sent to a provider to capture money.</summary>
/// <param name="OrderId">Order being paid (used as the idempotency key and the reference).</param>
/// <param name="Amount">Amount to capture.</param>
/// <param name="Method">Payment instrument.</param>
/// <param name="Description">Description shown to the customer.</param>
/// <param name="CustomerEmail">Customer e-mail.</param>
/// <param name="CustomerName">Customer name.</param>
/// <param name="CustomerDocument">Customer tax id (CPF/CNPJ), when the provider needs it.</param>
/// <param name="CardToken">Tokenized card produced by the provider's own SDK (never a card number).</param>
/// <param name="Installments">Number of installments (1 for the other methods).</param>
/// <param name="IdempotencyKey">Key that prevents duplicated charges.</param>
/// <param name="CallbackUrl">URL the provider calls back with status updates.</param>
/// <param name="Metadata">Extra key/value pairs forwarded to the provider.</param>
public sealed record ProviderChargeRequest(
    Guid OrderId,
    Money Amount,
    PaymentMethod Method,
    string Description,
    string? CustomerEmail,
    string? CustomerName,
    string? CustomerDocument,
    string? CardToken,
    int Installments,
    string IdempotencyKey,
    string? CallbackUrl,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Outcome of a charge, already translated to the domain vocabulary.</summary>
/// <param name="ProviderPaymentId">Identifier assigned by the provider.</param>
/// <param name="Status">Domain status of the payment.</param>
/// <param name="ProviderStatus">Raw provider status, kept for auditing.</param>
/// <param name="CheckoutUrl">Hosted checkout URL, when the provider returns one.</param>
/// <param name="PixCopyPasteCode">Pix copy-and-paste code, when the provider returns one.</param>
/// <param name="PixQrCodeBase64">Pix QR code (base64 image), when the provider returns one.</param>
/// <param name="FailureReason">Why the charge failed, when it did.</param>
/// <param name="RawResponse">Raw provider payload, kept for debugging.</param>
public sealed record ProviderChargeResult(
    string ProviderPaymentId,
    PaymentStatus Status,
    string? ProviderStatus,
    string? CheckoutUrl = null,
    string? PixCopyPasteCode = null,
    string? PixQrCodeBase64 = null,
    string? FailureReason = null,
    string? RawResponse = null);

/// <summary>Outcome of a refund.</summary>
/// <param name="Succeeded">Whether the provider accepted the refund.</param>
/// <param name="ProviderRefundId">Identifier assigned by the provider.</param>
/// <param name="ProviderStatus">Raw provider status.</param>
/// <param name="FailureReason">Why the refund failed, when it did.</param>
/// <param name="RawResponse">Raw provider payload, kept for debugging.</param>
public sealed record ProviderRefundResult(
    bool Succeeded,
    string? ProviderRefundId,
    string? ProviderStatus,
    string? FailureReason = null,
    string? RawResponse = null);

/// <summary>Public description of a provider, used to render the test screen.</summary>
/// <param name="Provider">Provider identity.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Methods">Accepted payment instruments.</param>
/// <param name="IsConfigured">Whether the provider has the credentials it needs.</param>
/// <param name="IsSimulation">Whether the provider is the offline simulator.</param>
public sealed record PaymentProviderDescriptor(
    PaymentProvider Provider,
    string DisplayName,
    IReadOnlyCollection<PaymentMethod> Methods,
    bool IsConfigured,
    bool IsSimulation);
