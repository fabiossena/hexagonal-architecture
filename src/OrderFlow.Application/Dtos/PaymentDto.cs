namespace OrderFlow.Application.Dtos;

/// <summary>Read model returned by the payment use cases.</summary>
/// <param name="Id">Payment identity.</param>
/// <param name="OrderId">Order being paid.</param>
/// <param name="Provider">Provider that processed the charge.</param>
/// <param name="Method">Payment instrument.</param>
/// <param name="Amount">Amount to capture.</param>
/// <param name="Status">Current status.</param>
/// <param name="Description">Description sent to the provider.</param>
/// <param name="CustomerEmail">Customer e-mail.</param>
/// <param name="ProviderPaymentId">Identifier assigned by the provider.</param>
/// <param name="ProviderStatus">Raw provider status.</param>
/// <param name="CheckoutUrl">Hosted checkout URL, when the provider returns one.</param>
/// <param name="PixCopyPasteCode">Pix copy-and-paste code, when the provider returns one.</param>
/// <param name="PixQrCodeBase64">Pix QR code (base64 image), when the provider returns one.</param>
/// <param name="FailureReason">Reason recorded when the payment failed.</param>
/// <param name="RefundedAmount">Amount refunded so far.</param>
/// <param name="RefundableAmount">Amount that can still be refunded.</param>
/// <param name="CreatedAtUtc">Creation instant (UTC).</param>
/// <param name="UpdatedAtUtc">Last update instant (UTC).</param>
/// <param name="PaidAtUtc">Capture instant (UTC), when applicable.</param>
/// <param name="CancelledAtUtc">Cancellation instant (UTC), when applicable.</param>
/// <param name="ProviderPayload">Raw provider payload, kept for debugging.</param>
public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    string Provider,
    string Method,
    MoneyDto Amount,
    string Status,
    string Description,
    string? CustomerEmail,
    string? ProviderPaymentId,
    string? ProviderStatus,
    string? CheckoutUrl,
    string? PixCopyPasteCode,
    string? PixQrCodeBase64,
    string? FailureReason,
    MoneyDto RefundedAmount,
    MoneyDto RefundableAmount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? PaidAtUtc,
    DateTime? CancelledAtUtc,
    string? ProviderPayload);
