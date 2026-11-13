using OrderFlow.Application.Dtos;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Orders;

/// <summary>
/// Maps the domain aggregate to the read model returned by the use cases.
/// </summary>
internal static class OrderMapper
{
    public static OrderDto ToDto(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.CreatedAtUtc,
            order.ConfirmedAtUtc,
            order.CancelledAtUtc,
            order.CancellationReason,
            order.Items
                .Select(item => new OrderItemDto(
                    item.Id,
                    item.ProductName,
                    item.Quantity,
                    ToDto(item.UnitPrice),
                    ToDto(item.LineTotal)))
                .ToList(),
            ToDto(order.Subtotal),
            ToDto(order.Discount),
            ToDto(order.Total));
    }

    private static MoneyDto ToDto(Money money) => new(money.Amount, money.Currency);
}
