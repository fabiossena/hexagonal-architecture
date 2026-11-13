using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Abstractions;

/// <summary>
/// Outbound port for order persistence.
/// </summary>
/// <remarks>
/// Declared by the application layer and implemented by an outbound adapter
/// (EF Core/PostgreSQL). The application never knows how persistence works.
/// </remarks>
public interface IOrderRepository
{
    /// <summary>Stages a new order for persistence.</summary>
    /// <param name="order">Aggregate to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(Order order, CancellationToken cancellationToken);

    /// <summary>Loads an order (with its items) by identity.</summary>
    /// <param name="id">Order identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order, or <c>null</c> when it does not exist.</returns>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads an order by its reference in an external system.</summary>
    /// <param name="externalReference">External reference (for example a quote id).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order, or <c>null</c> when it does not exist.</returns>
    Task<Order?> GetByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken);

    /// <summary>Lists orders, newest first.</summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="skip">Number of rows to skip.</param>
    /// <param name="take">Maximum number of rows.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Order>> ListAsync(
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Counts orders matching the optional status filter.</summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> CountAsync(OrderStatus? status, CancellationToken cancellationToken);
}
