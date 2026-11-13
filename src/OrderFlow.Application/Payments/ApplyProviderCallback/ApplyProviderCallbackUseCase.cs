using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Application.Payments.ApplyProviderCallback;

/// <summary>Input of <see cref="ApplyProviderCallbackUseCase"/>.</summary>
/// <param name="Provider">Provider that sent the notification.</param>
/// <param name="ProviderPaymentId">Provider reference of the payment.</param>
/// <param name="ProviderStatus">Raw status reported by the provider.</param>
public sealed record ProviderCallbackCommand(
    PaymentProvider Provider,
    string ProviderPaymentId,
    string ProviderStatus);

/// <summary>
/// Use case: apply a status update sent by a payment provider.
/// </summary>
/// <remarks>
/// The raw provider status is translated by the provider adapter itself, so this
/// use case never has to know the vendor vocabulary. An unknown payment is
/// ignored on purpose: providers retry their notifications and the payment may
/// not exist yet.
/// </remarks>
public sealed class ApplyProviderCallbackUseCase
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
    public ApplyProviderCallbackUseCase(
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

    /// <summary>Applies the callback to the matching payment.</summary>
    /// <param name="command">Callback data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a payment was updated, <c>false</c> when it is unknown.</returns>
    public async Task<bool> ExecuteAsync(
        ProviderCallbackCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.ProviderPaymentId))
        {
            return false;
        }

        var payment = await _payments.GetByProviderPaymentIdAsync(command.ProviderPaymentId, cancellationToken);
        if (payment is null)
        {
            return false;
        }

        var provider = _providers.Resolve(command.Provider);
        var status = provider.MapStatus(command.ProviderStatus);

        payment.SyncStatus(status, command.ProviderPaymentId, command.ProviderStatus, _clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
