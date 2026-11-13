using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Payments.CancelPayment;

/// <summary>
/// Use case: cancel a payment that has not been captured yet.
/// </summary>
/// <remarks>
/// The cancellation is local: the charge is simply abandoned, so the customer
/// never completes it. Providers that support explicit cancellation would be
/// called here.
/// </remarks>
public sealed class CancelPaymentUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the use case.</summary>
    /// <param name="payments">Payment repository port.</param>
    /// <param name="unitOfWork">Transaction port.</param>
    /// <param name="clock">Clock port.</param>
    public CancelPaymentUseCase(IPaymentRepository payments, IUnitOfWork unitOfWork, IClock clock)
    {
        _payments = payments;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Cancels the payment.</summary>
    /// <param name="paymentId">Payment identity.</param>
    /// <param name="reason">Why the payment is being cancelled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancelled payment as a read model.</returns>
    /// <exception cref="PaymentNotFoundException">The payment does not exist.</exception>
    /// <exception cref="Domain.Exceptions.ValidationException">The reason is missing.</exception>
    /// <exception cref="Domain.Exceptions.ConflictException">The payment cannot be cancelled.</exception>
    public async Task<PaymentDto> ExecuteAsync(
        Guid paymentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(paymentId);

        payment.Cancel(reason, _clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PaymentMapper.ToDto(payment);
    }
}
