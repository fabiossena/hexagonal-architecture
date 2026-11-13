namespace OrderFlow.Adapters.Inbound.Api.Contracts;

/// <summary>Line sent in <see cref="PlaceOrderRequest"/>.</summary>
/// <param name="ProductName">Product name.</param>
/// <param name="Quantity">Quantity (1..1000).</param>
/// <param name="UnitPrice">Unit price in the order currency.</param>
public sealed record PlaceOrderItemRequest(string? ProductName, int Quantity, decimal UnitPrice);

/// <summary>Body of <c>POST /api/orders</c>.</summary>
/// <param name="CustomerId">Customer placing the order.</param>
/// <param name="Currency">Optional ISO-4217 currency (defaults to USD).</param>
/// <param name="ExternalReference">Optional reference of the order in an external system (a quote id).</param>
/// <param name="Items">At least one line.</param>
public sealed record PlaceOrderRequest(
    Guid CustomerId,
    string? Currency,
    string? ExternalReference,
    IReadOnlyList<PlaceOrderItemRequest>? Items);

/// <summary>Body of <c>POST /api/orders/{id}/cancel</c>.</summary>
/// <param name="Reason">Why the order is being cancelled.</param>
public sealed record CancelOrderRequest(string? Reason);
