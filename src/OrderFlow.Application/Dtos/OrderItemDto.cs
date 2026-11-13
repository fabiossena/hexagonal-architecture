namespace OrderFlow.Application.Dtos;

/// <summary>A single order line exposed by the API.</summary>
/// <param name="Id">Item identity.</param>
/// <param name="ProductName">Product name.</param>
/// <param name="Quantity">Quantity ordered.</param>
/// <param name="UnitPrice">Unit price.</param>
/// <param name="LineTotal">Unit price multiplied by the quantity.</param>
public sealed record OrderItemDto(
    Guid Id,
    string ProductName,
    int Quantity,
    MoneyDto UnitPrice,
    MoneyDto LineTotal);
