using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders.ListOrders;

/// <summary>Input of <see cref="ListOrdersUseCase"/>.</summary>
/// <param name="Page">One-based page number (clamped to ≥ 1).</param>
/// <param name="Size">Page size (clamped to 1..100).</param>
/// <param name="Status">Optional status filter.</param>
public sealed record ListOrdersQuery(int Page = 1, int Size = 20, OrderStatus? Status = null);

/// <summary>
/// Use case: list orders with pagination and an optional status filter.
/// </summary>
public sealed class ListOrdersUseCase
{
    /// <summary>Maximum page size accepted.</summary>
    public const int MaxPageSize = 100;

    private readonly IOrderRepository _orders;

    /// <summary>Creates the use case.</summary>
    /// <param name="orders">Order repository port.</param>
    public ListOrdersUseCase(IOrderRepository orders)
    {
        _orders = orders;
    }

    /// <summary>Returns a page of orders.</summary>
    /// <param name="query">Paging and filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The paged result.</returns>
    public async Task<PagedResult<OrderDto>> ExecuteAsync(
        ListOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page < 1 ? 1 : query.Page;
        var size = Math.Clamp(query.Size, 1, MaxPageSize);

        var totalCount = await _orders.CountAsync(query.Status, cancellationToken);
        var orders = await _orders.ListAsync(query.Status, (page - 1) * size, size, cancellationToken);

        return new PagedResult<OrderDto>(
            orders.Select(OrderMapper.ToDto).ToList(),
            page,
            size,
            totalCount);
    }
}
