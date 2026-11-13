namespace OrderFlow.Application.Dtos;

/// <summary>Read model returned by the order use cases.</summary>
/// <param name="Id">Order identity.</param>
/// <param name="CustomerId">Customer identity.</param>
/// <param name="ExternalReference">Reference of the order in an external system (optional).</param>
/// <param name="Status">Current status (<c>Draft</c>, <c>Confirmed</c>, <c>Cancelled</c>).</param>
/// <param name="CreatedAtUtc">Creation instant (UTC).</param>
/// <param name="ConfirmedAtUtc">Confirmation instant (UTC), when applicable.</param>
/// <param name="CancelledAtUtc">Cancellation instant (UTC), when applicable.</param>
/// <param name="CancellationReason">Reason recorded on cancellation, when applicable.</param>
/// <param name="Items">Order lines.</param>
/// <param name="Subtotal">Sum of the lines.</param>
/// <param name="Discount">Volume discount applied.</param>
/// <param name="Total">Amount due.</param>
public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string? ExternalReference,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    IReadOnlyList<OrderItemDto> Items,
    MoneyDto Subtotal,
    MoneyDto Discount,
    MoneyDto Total);
