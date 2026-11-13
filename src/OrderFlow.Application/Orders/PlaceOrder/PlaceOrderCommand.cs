using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Orders.PlaceOrder;

/// <summary>Line requested when placing an order.</summary>
/// <param name="ProductName">Product name.</param>
/// <param name="Quantity">Quantity (1..1000).</param>
/// <param name="UnitPrice">Unit price in the order currency.</param>
public sealed record PlaceOrderItem(string ProductName, int Quantity, decimal UnitPrice);

/// <summary>Input of <see cref="PlaceOrderUseCase"/>.</summary>
/// <param name="CustomerId">Customer placing the order.</param>
/// <param name="Items">At least one line.</param>
/// <param name="Currency">ISO-4217 currency shared by every line.</param>
public sealed record PlaceOrderCommand(
    Guid CustomerId,
    IReadOnlyList<PlaceOrderItem> Items,
    string Currency = Money.DefaultCurrency);
