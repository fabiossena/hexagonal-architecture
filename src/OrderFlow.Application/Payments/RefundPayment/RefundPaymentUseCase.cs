using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Payments.RefundPayment;

/// <summary>
/// Use case: refund a captured payment.
/// </summary>
public sealed class RefundPaymentUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProviderResolver _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the use case.</summary>
    /// <param name="payments">Payment repository port.</param>
    /// <param name="providers">Provider resolver port.</param>
    /// <param name="unitOfWork">Transaction port.</param>
    /// <param name="clock">Clock port.</param>
    public RefundPaymentUseCase(
        IPaymentRepository payments,
        IPaymentProviderResolver providers,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _payments = payments;
        _providers = providers;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Refunds the payment (fully when no amount is given).</summary>
    /// <param name="paymentId">Payment identity.</param>
    /// <param name="amount">Amount to refund (defaults to the refundable amount).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated payment as a read model.</returns>
    /// <exception cref="PaymentNotFoundException">The payment does not exist.</exception>
    /// <exception cref="ConflictException">The payment cannot be refunded.</exception>
    /// <exception cref="ValidationException">The amount is invalid.</exception>
    /// <exception cref="PaymentProviderException">The provider refused the refund.</exception>
    public async Task<PaymentDto> ExecuteAsync(
        Guid paymentId,
        decimal? amount = null,
        CancellationToken cancellationToken = default)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(paymentId);

        if (string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
        {
            throw new ConflictException("The payment was never sent to the provider and cannot be refunded.");
        }

        var provider = _providers.Resolve(payment.Provider);

        if (!provider.IsConfigured)
        {
            throw new ValidationException(
                $"Provider '{provider.DisplayName}' is not configured. Set its credentials in the configuration.");
        }

        var refundAmount = amount is null
            ? payment.RefundableAmount
            : new Money(amount.Value, payment.Amount.Currency);

        if (refundAmount.Amount <= 0m)
        {
            throw new ValidationException("The refund amount must be greater than zero.");
        }

        var result = await provider.RefundAsync(
            payment.ProviderPaymentId,
            refundAmount,
            cancellationToken);

        if (!result.Succeeded)
        {
            payment.AttachProviderPayload(result.RawResponse);

            throw new PaymentProviderException(
                result.FailureReason ?? "The provider refused the refund.",
                result.RawResponse);
        }

        payment.RegisterRefund(refundAmount, _clock.UtcNow);
        payment.AttachProviderPayload(result.RawResponse);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PaymentMapper.ToDto(payment);
    }
}
