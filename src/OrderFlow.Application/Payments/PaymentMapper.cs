using OrderFlow.Application.Dtos;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Payments;

/// <summary>Maps the payment aggregate to the read model returned by the use cases.</summary>
internal static class PaymentMapper
{
    public static PaymentDto ToDto(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return new PaymentDto(
            payment.Id,
            payment.OrderId,
            payment.Provider.ToString(),
            payment.Method.ToString(),
            ToDto(payment.Amount),
            payment.Status.ToString(),
            payment.Description,
            payment.CustomerEmail,
            payment.ProviderPaymentId,
            payment.ProviderStatus,
            payment.CheckoutUrl,
            payment.PixCopyPasteCode,
            payment.PixQrCodeBase64,
            payment.FailureReason,
            ToDto(payment.RefundedAmount),
            ToDto(payment.RefundableAmount),
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc,
            payment.PaidAtUtc,
            payment.CancelledAtUtc,
            payment.ProviderPayload);
    }

    private static MoneyDto ToDto(Money money) => new(money.Amount, money.Currency);
}
