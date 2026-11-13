using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Orders.GetOrder;

/// <summary>
/// Use case: read a single order.
/// </summary>
public sealed class GetOrderUseCase
{
    private readonly IOrderRepository _orders;

    /// <summary>Creates the use case.</summary>
    /// <param name="orders">Order repository port.</param>
    public GetOrderUseCase(IOrderRepository orders)
    {
        _orders = orders;
    }

    /// <summary>Loads an order by identity.</summary>
    /// <param name="orderId">Order identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order.</returns>
    /// <exception cref="OrderNotFoundException">The order does not exist.</exception>
    public async Task<OrderDto> ExecuteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

        return OrderMapper.ToDto(order);
    }
}
