using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Orders.PlaceOrder;

/// <summary>
/// Use case: create a draft order with one or more lines.
/// </summary>
/// <remarks>
/// Orchestration only — every invariant is enforced by the aggregate. The use
/// case depends on outbound ports (<see cref="IOrderRepository"/>,
/// <see cref="IUnitOfWork"/>, <see cref="IClock"/>), never on a concrete
/// database or clock.
/// </remarks>
public sealed class PlaceOrderUseCase
{
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the use case.</summary>
    /// <param name="orders">Order repository port.</param>
    /// <param name="unitOfWork">Transaction port.</param>
    /// <param name="clock">Clock port.</param>
    public PlaceOrderUseCase(IOrderRepository orders, IUnitOfWork unitOfWork, IClock clock)
    {
        _orders = orders;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Creates, persists and returns the new order.</summary>
    /// <param name="command">Order data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted order as a read model.</returns>
    /// <exception cref="ValidationException">The command is invalid.</exception>
    public async Task<OrderDto> ExecuteAsync(
        PlaceOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Items is null || command.Items.Count == 0)
        {
            throw new ValidationException("An order must contain at least one item.");
        }

        var order = Order.Create(command.CustomerId, _clock.UtcNow, command.ExternalReference);

        foreach (var item in command.Items)
        {
            if (item is null)
            {
                throw new ValidationException("Order items cannot be null.");
            }

            order.AddItem(item.ProductName, item.Quantity, new Money(item.UnitPrice, command.Currency));
        }

        await _orders.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OrderMapper.ToDto(order);
    }
}
