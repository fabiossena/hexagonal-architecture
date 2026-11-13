using OrderFlow.Domain.Payments;

namespace OrderFlow.Application.Abstractions;

/// <summary>
/// Outbound port for payment persistence.
/// </summary>
/// <remarks>
/// Declared by the application layer and implemented by an outbound adapter
/// (EF Core/PostgreSQL). The application never knows how persistence works.
/// </remarks>
public interface IPaymentRepository
{
    /// <summary>Stages a new payment for persistence.</summary>
    /// <param name="payment">Aggregate to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    /// <summary>Loads a payment by identity.</summary>
    /// <param name="id">Payment identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payment, or <c>null</c> when it does not exist.</returns>
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a payment by the identifier assigned by the provider.</summary>
    /// <param name="providerPaymentId">Provider reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payment, or <c>null</c> when it does not exist.</returns>
    Task<Payment?> GetByProviderPaymentIdAsync(string providerPaymentId, CancellationToken cancellationToken);

    /// <summary>Lists payments, newest first.</summary>
    /// <param name="orderId">Optional order filter.</param>
    /// <param name="provider">Optional provider filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="skip">Number of rows to skip.</param>
    /// <param name="take">Maximum number of rows.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Payment>> ListAsync(
        Guid? orderId,
        PaymentProvider? provider,
        PaymentStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Counts payments matching the optional filters.</summary>
    /// <param name="orderId">Optional order filter.</param>
    /// <param name="provider">Optional provider filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> CountAsync(
        Guid? orderId,
        PaymentProvider? provider,
        PaymentStatus? status,
        CancellationToken cancellationToken);
}
