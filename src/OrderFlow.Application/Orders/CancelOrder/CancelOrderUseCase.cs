using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Orders.CancelOrder;

/// <summary>
/// Use case: cancel a draft or confirmed order, recording the reason.
/// </summary>
public sealed class CancelOrderUseCase
{
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the use case.</summary>
    /// <param name="orders">Order repository port.</param>
    /// <param name="unitOfWork">Transaction port.</param>
    /// <param name="clock">Clock port.</param>
    public CancelOrderUseCase(IOrderRepository orders, IUnitOfWork unitOfWork, IClock clock)
    {
        _orders = orders;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Cancels the order and commits the change.</summary>
    /// <param name="orderId">Order identity.</param>
    /// <param name="reason">Cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated order.</returns>
    /// <exception cref="OrderNotFoundException">The order does not exist.</exception>
    /// <exception cref="Domain.Exceptions.ConflictException">The order is already cancelled.</exception>
    /// <exception cref="Domain.Exceptions.ValidationException">The reason is missing.</exception>
    public async Task<OrderDto> ExecuteAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

        order.Cancel(reason, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OrderMapper.ToDto(order);
    }
}
