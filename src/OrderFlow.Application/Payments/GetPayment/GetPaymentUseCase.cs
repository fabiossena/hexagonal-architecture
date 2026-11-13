using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Payments.GetPayment;

/// <summary>
/// Use case: read a single payment.
/// </summary>
public sealed class GetPaymentUseCase
{
    private readonly IPaymentRepository _payments;

    /// <summary>Creates the use case.</summary>
    /// <param name="payments">Payment repository port.</param>
    public GetPaymentUseCase(IPaymentRepository payments)
    {
        _payments = payments;
    }

    /// <summary>Returns the payment with the given identity.</summary>
    /// <param name="paymentId">Payment identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payment as a read model.</returns>
    /// <exception cref="PaymentNotFoundException">The payment does not exist.</exception>
    public async Task<PaymentDto> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(paymentId);

        return PaymentMapper.ToDto(payment);
    }
}
