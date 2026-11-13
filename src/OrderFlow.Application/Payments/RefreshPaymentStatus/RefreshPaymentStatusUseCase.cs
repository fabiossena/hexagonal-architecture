using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Payments.RefreshPaymentStatus;

/// <summary>
/// Use case: ask the provider for the current state of a payment and sync it.
/// </summary>
/// <remarks>
/// This is what the test screen calls to see a Pix or Boleto payment move from
/// <c>Pending</c> to <c>Paid</c>.
/// </remarks>
public sealed class RefreshPaymentStatusUseCase
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
    public RefreshPaymentStatusUseCase(
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

    /// <summary>Refreshes the payment status from the provider.</summary>
    /// <param name="paymentId">Payment identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated payment as a read model.</returns>
    /// <exception cref="PaymentNotFoundException">The payment does not exist.</exception>
    /// <exception cref="Domain.Exceptions.ConflictException">The payment was never sent to the provider.</exception>
    /// <exception cref="PaymentProviderException">The provider call failed.</exception>
    public async Task<PaymentDto> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(paymentId);

        if (string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
        {
            throw new Domain.Exceptions.ConflictException(
                "The payment was never sent to the provider, so its status cannot be refreshed.");
        }

        var provider = _providers.Resolve(payment.Provider);

        if (!provider.IsConfigured)
        {
            throw new Domain.Exceptions.ValidationException(
                $"Provider '{provider.DisplayName}' is not configured. Set its credentials in the configuration.");
        }

        var result = await provider.GetChargeAsync(payment.ProviderPaymentId, cancellationToken);

        payment.SyncStatus(result.Status, result.ProviderPaymentId, result.ProviderStatus, _clock.UtcNow);
        payment.AttachCheckoutDetails(result.CheckoutUrl, result.PixCopyPasteCode, result.PixQrCodeBase64);
        payment.AttachProviderPayload(result.RawResponse);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PaymentMapper.ToDto(payment);
    }
}
