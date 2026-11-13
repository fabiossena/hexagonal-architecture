using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Payments;

/// <summary>
/// Payment aggregate root: one payment attempt against an order.
/// </summary>
/// <remarks>
/// <para>
/// The aggregate owns the invariants of the payment lifecycle and is the only
/// entry point for changing it. The provider call itself happens in the
/// application layer; the provider result is translated into the granular
/// transitions exposed below (<see cref="MarkProcessing"/>, <see cref="MarkPaid"/>,
/// <see cref="MarkFailed"/>).
/// </para>
/// <para>
/// Business rules:
/// </para>
/// <list type="bullet">
///   <item><description>the amount must be strictly positive;</description></item>
///   <item><description>the provider must accept the chosen method (see <see cref="PaymentPolicy"/>);</description></item>
///   <item><description>a payment can only be paid, failed or cancelled while it is pending or processing;</description></item>
///   <item><description>only a paid payment can be refunded, and never more than the captured amount;</description></item>
///   <item><description>failed, cancelled and refunded payments are terminal.</description></item>
/// </list>
/// </remarks>
public sealed class Payment
{
    /// <summary>Maximum length accepted for the description.</summary>
    public const int MaxDescriptionLength = 200;

    /// <summary>Maximum length accepted for the customer e-mail.</summary>
    public const int MaxCustomerEmailLength = 320;

    /// <summary>Maximum length accepted for a provider reference.</summary>
    public const int MaxProviderReferenceLength = 200;

    /// <summary>Maximum length accepted for the raw provider payload.</summary>
    public const int MaxProviderPayloadLength = 8000;

    // Required by EF Core materialization.
    private Payment()
    {
    }

    private Payment(
        Guid id,
        Guid orderId,
        PaymentProvider provider,
        PaymentMethod method,
        Money amount,
        string description,
        string? customerEmail,
        DateTime createdAtUtc)
    {
        Id = id;
        OrderId = orderId;
        Provider = provider;
        Method = method;
        Amount = amount;
        Description = description;
        CustomerEmail = customerEmail;
        Status = PaymentStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    /// <summary>Gets the payment identity.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the order this payment settles.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the provider that processes the charge.</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>Gets the payment instrument.</summary>
    public PaymentMethod Method { get; private set; }

    /// <summary>Gets the amount to be captured.</summary>
    public Money Amount { get; private set; } = Money.Zero();

    /// <summary>Gets the current status.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>Gets the description sent to the provider.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the customer e-mail used for the charge.</summary>
    public string? CustomerEmail { get; private set; }

    /// <summary>Gets the identifier assigned by the provider, when known.</summary>
    public string? ProviderPaymentId { get; private set; }

    /// <summary>Gets the raw status returned by the provider.</summary>
    public string? ProviderStatus { get; private set; }

    /// <summary>Gets the hosted checkout URL, when the provider returns one.</summary>
    public string? CheckoutUrl { get; private set; }

    /// <summary>Gets the Pix copy-and-paste code, when the provider returns one.</summary>
    public string? PixCopyPasteCode { get; private set; }

    /// <summary>Gets the Pix QR code (base64 image), when the provider returns one.</summary>
    public string? PixQrCodeBase64 { get; private set; }

    /// <summary>Gets the reason recorded when the payment failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Gets the raw payload returned by the provider, kept for auditing.</summary>
    public string? ProviderPayload { get; private set; }

    /// <summary>Gets the total amount refunded so far (raw value).</summary>
    public decimal RefundedAmountValue { get; private set; }

    /// <summary>Gets the total amount refunded so far.</summary>
    public Money RefundedAmount => new(RefundedAmountValue, Amount.Currency);

    /// <summary>Gets the amount that can still be refunded.</summary>
    public Money RefundableAmount => new(Amount.Amount - RefundedAmountValue, Amount.Currency);

    /// <summary>Gets when the payment was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Gets when the payment was last updated (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Gets when the money was captured (UTC), if it was.</summary>
    public DateTime? PaidAtUtc { get; private set; }

    /// <summary>Gets when the payment was cancelled (UTC), if it was.</summary>
    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Gets a value indicating whether the payment reached a final status.</summary>
    public bool IsTerminal => PaymentPolicy.IsTerminal(Status);

    /// <summary>
    /// Creates a pending payment for an order.
    /// </summary>
    /// <param name="orderId">Order being paid.</param>
    /// <param name="provider">Provider that will process the charge.</param>
    /// <param name="method">Payment instrument.</param>
    /// <param name="amount">Amount to capture (strictly positive).</param>
    /// <param name="description">Description shown to the customer.</param>
    /// <param name="customerEmail">Customer e-mail (optional).</param>
    /// <param name="nowUtc">Current UTC instant, supplied by the application layer (<c>IClock</c>).</param>
    /// <returns>A new pending <see cref="Payment"/>.</returns>
    /// <exception cref="ValidationException">The data is invalid or the provider does not accept the method.</exception>
    public static Payment Create(
        Guid orderId,
        PaymentProvider provider,
        PaymentMethod method,
        Money amount,
        string description,
        string? customerEmail,
        DateTime nowUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new ValidationException("OrderId is required.");
        }

        if (!PaymentPolicy.Supports(provider, method))
        {
            throw new ValidationException($"Provider '{provider}' does not support the method '{method}'.");
        }

        ArgumentNullException.ThrowIfNull(amount);

        if (amount.Amount <= 0m)
        {
            throw new ValidationException("The payment amount must be greater than zero.");
        }

        var normalizedDescription = NormalizeDescription(description);
        var normalizedEmail = NormalizeEmail(customerEmail);

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ValidationException("The creation instant must be expressed in UTC.");
        }

        return new Payment(
            Guid.NewGuid(),
            orderId,
            provider,
            method,
            amount,
            normalizedDescription,
            normalizedEmail,
            nowUtc);
    }

    /// <summary>
    /// Marks the payment as accepted by the provider and still being processed.
    /// </summary>
    /// <param name="providerPaymentId">Identifier assigned by the provider.</param>
    /// <param name="providerStatus">Raw provider status.</param>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <exception cref="ConflictException">The payment is not in a state that allows the transition.</exception>
    public void MarkProcessing(string providerPaymentId, string providerStatus, DateTime nowUtc)
    {
        EnsureTransition(PaymentStatus.Processing);

        ProviderPaymentId = NormalizeProviderReference(providerPaymentId);
        ProviderStatus = NormalizeProviderStatus(providerStatus);
        Status = PaymentStatus.Processing;
        Touch(nowUtc);
    }

    /// <summary>
    /// Marks the payment as captured.
    /// </summary>
    /// <param name="providerPaymentId">Identifier assigned by the provider.</param>
    /// <param name="providerStatus">Raw provider status.</param>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <exception cref="ConflictException">The payment is not in a state that allows the transition.</exception>
    public void MarkPaid(string providerPaymentId, string providerStatus, DateTime nowUtc)
    {
        EnsureTransition(PaymentStatus.Paid);

        ProviderPaymentId = NormalizeProviderReference(providerPaymentId) ?? ProviderPaymentId;
        ProviderStatus = NormalizeProviderStatus(providerStatus);
        Status = PaymentStatus.Paid;
        PaidAtUtc = EnsureUtc(nowUtc);
        Touch(nowUtc);
    }

    /// <summary>
    /// Marks the payment as failed.
    /// </summary>
    /// <param name="providerPaymentId">Identifier assigned by the provider, when known.</param>
    /// <param name="providerStatus">Raw provider status.</param>
    /// <param name="reason">Why the payment failed.</param>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <exception cref="ConflictException">The payment is not in a state that allows the transition.</exception>
    public void MarkFailed(string? providerPaymentId, string? providerStatus, string reason, DateTime nowUtc)
    {
        EnsureTransition(PaymentStatus.Failed);

        ProviderPaymentId = NormalizeProviderReference(providerPaymentId) ?? ProviderPaymentId;
        ProviderStatus = NormalizeProviderStatus(providerStatus);
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "The provider rejected the charge." : reason.Trim();
        Status = PaymentStatus.Failed;
        Touch(nowUtc);
    }

    /// <summary>
    /// Attaches the artefacts returned by the provider (checkout URL, Pix code/QR).
    /// </summary>
    /// <param name="checkoutUrl">Hosted checkout URL.</param>
    /// <param name="pixCopyPasteCode">Pix copy-and-paste code.</param>
    /// <param name="pixQrCodeBase64">Pix QR code as a base64 image.</param>
    public void AttachCheckoutDetails(string? checkoutUrl, string? pixCopyPasteCode, string? pixQrCodeBase64)
    {
        if (!string.IsNullOrWhiteSpace(checkoutUrl))
        {
            CheckoutUrl = checkoutUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(pixCopyPasteCode))
        {
            PixCopyPasteCode = pixCopyPasteCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(pixQrCodeBase64))
        {
            PixQrCodeBase64 = pixQrCodeBase64.Trim();
        }
    }

    /// <summary>
    /// Attaches the raw payload returned by the provider (kept for debugging).
    /// </summary>
    /// <param name="rawResponse">Raw provider payload.</param>
    public void AttachProviderPayload(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return;
        }

        var normalized = rawResponse.Trim();
        ProviderPayload = normalized.Length > MaxProviderPayloadLength
            ? normalized[..MaxProviderPayloadLength]
            : normalized;
    }

    /// <summary>
    /// Synchronises the payment with the latest status reported by the provider.
    /// </summary>
    /// <param name="status">Status reported by the provider.</param>
    /// <param name="providerPaymentId">Identifier assigned by the provider.</param>
    /// <param name="providerStatus">Raw provider status.</param>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <remarks>
    /// Only transitions allowed by <see cref="PaymentPolicy"/> are applied: a
    /// terminal status is never overwritten, and an unchanged status only
    /// refreshes the provider metadata.
    /// </remarks>
    public void SyncStatus(
        PaymentStatus status,
        string? providerPaymentId,
        string? providerStatus,
        DateTime nowUtc)
    {
        ProviderPaymentId = NormalizeProviderReference(providerPaymentId) ?? ProviderPaymentId;
        ProviderStatus = NormalizeProviderStatus(providerStatus) ?? ProviderStatus;

        if (status != Status && PaymentPolicy.CanTransition(Status, status))
        {
            switch (status)
            {
                case PaymentStatus.Processing:
                    Status = PaymentStatus.Processing;
                    break;
                case PaymentStatus.Paid:
                    Status = PaymentStatus.Paid;
                    PaidAtUtc = EnsureUtc(nowUtc);
                    break;
                case PaymentStatus.Failed:
                    Status = PaymentStatus.Failed;
                    break;
                case PaymentStatus.Cancelled:
                    Status = PaymentStatus.Cancelled;
                    CancelledAtUtc = EnsureUtc(nowUtc);
                    break;
                case PaymentStatus.Refunded:
                    Status = PaymentStatus.Refunded;
                    break;
                case PaymentStatus.Pending:
                default:
                    break;
            }
        }

        Touch(nowUtc);
    }

    /// <summary>
    /// Registers a refund against a captured payment.
    /// </summary>
    /// <param name="amount">Amount to refund (strictly positive, not exceeding the refundable amount).</param>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <exception cref="ConflictException">The payment is not in a state that allows the transition.</exception>
    /// <exception cref="ValidationException">The amount is invalid or exceeds the refundable amount.</exception>
    public void RegisterRefund(Money amount, DateTime nowUtc)
    {
        EnsureTransition(PaymentStatus.Refunded);

        ArgumentNullException.ThrowIfNull(amount);

        if (amount.Amount <= 0m)
        {
            throw new ValidationException("The refund amount must be greater than zero.");
        }

        if (!string.Equals(amount.Currency, Amount.Currency, StringComparison.Ordinal))
        {
            throw new ValidationException("The refund must use the same currency as the payment.");
        }

        if (amount.Amount > RefundableAmount.Amount)
        {
            throw new ValidationException(
                $"The refund amount exceeds the refundable amount ({RefundableAmount.Amount} {Amount.Currency}).");
        }

        RefundedAmountValue += amount.Amount;

        if (RefundedAmountValue >= Amount.Amount)
        {
            Status = PaymentStatus.Refunded;
        }

        Touch(nowUtc);
    }

    /// <summary>
    /// Cancels a payment that has not been captured yet.
    /// </summary>
    /// <param name="reason">Why the payment is being cancelled.</param>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <exception cref="ConflictException">The payment is not in a state that allows the transition.</exception>
    /// <exception cref="ValidationException">The reason is missing.</exception>
    public void Cancel(string reason, DateTime nowUtc)
    {
        EnsureTransition(PaymentStatus.Cancelled);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("A cancellation reason is required.");
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 500)
        {
            throw new ValidationException("The cancellation reason must be at most 500 characters.");
        }

        FailureReason = normalizedReason;
        Status = PaymentStatus.Cancelled;
        CancelledAtUtc = EnsureUtc(nowUtc);
        Touch(nowUtc);
    }

    private void EnsureTransition(PaymentStatus target)
    {
        if (!PaymentPolicy.CanTransition(Status, target))
        {
            throw new ConflictException(
                $"A payment in status '{Status}' cannot move to '{target}'.");
        }
    }

    private void Touch(DateTime nowUtc) => UpdatedAtUtc = EnsureUtc(nowUtc);

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ValidationException("The instant must be expressed in UTC.");
        }

        return value;
    }

    private static string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ValidationException("A description is required.");
        }

        var normalized = description.Trim();
        if (normalized.Length > MaxDescriptionLength)
        {
            throw new ValidationException($"The description must be at most {MaxDescriptionLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeEmail(string? customerEmail)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return null;
        }

        var normalized = customerEmail.Trim();
        if (normalized.Length > MaxCustomerEmailLength)
        {
            throw new ValidationException($"The customer e-mail must be at most {MaxCustomerEmailLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeProviderReference(string? providerPaymentId)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            return null;
        }

        var normalized = providerPaymentId.Trim();
        return normalized.Length > MaxProviderReferenceLength
            ? normalized[..MaxProviderReferenceLength]
            : normalized;
    }

    private static string? NormalizeProviderStatus(string? providerStatus)
    {
        if (string.IsNullOrWhiteSpace(providerStatus))
        {
            return null;
        }

        var normalized = providerStatus.Trim();
        return normalized.Length > MaxProviderReferenceLength
            ? normalized[..MaxProviderReferenceLength]
            : normalized;
    }
}
