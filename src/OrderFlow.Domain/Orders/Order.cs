using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Orders;

/// <summary>
/// Order aggregate root.
/// </summary>
/// <remarks>
/// <para>
/// The aggregate owns every invariant of the order lifecycle and is the only
/// entry point for changing it: callers cannot mutate items directly.
/// </para>
/// <para>
/// Business rules:
/// </para>
/// <list type="bullet">
///   <item><description>an order always has a customer and a UTC creation instant;</description></item>
///   <item><description>items can only be added or removed while the order is a draft;</description></item>
///   <item><description>every item must use the same currency;</description></item>
///   <item><description>a draft can only be confirmed if it has at least one item;</description></item>
///   <item><description>a draft or confirmed order can be cancelled, with a reason, once;</description></item>
///   <item><description>volume discounts are applied through <see cref="VolumeDiscountPolicy"/>.</description></item>
/// </list>
/// </remarks>
public sealed class Order
{
    private readonly List<OrderItem> _items = new();

    // Required by EF Core materialization.
    private Order()
    {
    }

    /// <summary>Gets the order identity.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the customer that placed the order.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>Gets when the order was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Gets when the order was confirmed (UTC), if it was.</summary>
    public DateTime? ConfirmedAtUtc { get; private set; }

    /// <summary>Gets when the order was cancelled (UTC), if it was.</summary>
    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Gets the reason recorded when the order was cancelled.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Gets the order items (read-only view of the aggregate state).</summary>
    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>Gets the sum of all line totals.</summary>
    public Money Subtotal =>
        _items.Count == 0
            ? Money.Zero()
            : _items.Aggregate(Money.Zero(_items[0].UnitPrice.Currency), (total, item) => total + item.LineTotal);

    /// <summary>Gets the volume discount granted for this order.</summary>
    public Money Discount => VolumeDiscountPolicy.Calculate(Subtotal, _items.Count);

    /// <summary>Gets the amount the customer has to pay (<see cref="Subtotal"/> − <see cref="Discount"/>).</summary>
    public Money Total => Subtotal - Discount;

    /// <summary>
    /// Creates a new draft order.
    /// </summary>
    /// <param name="customerId">Customer identity.</param>
    /// <param name="nowUtc">Current UTC instant, supplied by the application layer (<c>IClock</c>).</param>
    /// <returns>A new draft <see cref="Order"/>.</returns>
    /// <exception cref="ValidationException">The customer is empty or the instant is not UTC.</exception>
    public static Order Create(Guid customerId, DateTime nowUtc)
    {
        if (customerId == Guid.Empty)
        {
            throw new ValidationException("CustomerId is required.");
        }

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ValidationException("The creation instant must be expressed in UTC.");
        }

        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Draft,
            CreatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Adds an item to a draft order.
    /// </summary>
    /// <param name="productName">Product name.</param>
    /// <param name="quantity">Quantity (1..1000).</param>
    /// <param name="unitPrice">Unit price in the order currency.</param>
    /// <exception cref="ConflictException">The order is no longer a draft.</exception>
    /// <exception cref="ValidationException">The item data is invalid or mixes currencies.</exception>
    public void AddItem(string productName, int quantity, Money unitPrice)
    {
        EnsureDraft("add items");
        ArgumentNullException.ThrowIfNull(unitPrice);

        if (_items.Count > 0 &&
            !string.Equals(_items[0].UnitPrice.Currency, unitPrice.Currency, StringComparison.Ordinal))
        {
            throw new ValidationException("All items in an order must share the same currency.");
        }

        _items.Add(OrderItem.Create(productName, quantity, unitPrice));
    }

    /// <summary>
    /// Removes an item from a draft order.
    /// </summary>
    /// <param name="itemId">Identity of the item to remove.</param>
    /// <exception cref="ConflictException">The order is no longer a draft.</exception>
    /// <exception cref="ValidationException">The item does not belong to this order.</exception>
    public void RemoveItem(Guid itemId)
    {
        EnsureDraft("remove items");

        var item = _items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            throw new ValidationException($"Item '{itemId}' does not belong to order '{Id}'.");
        }

        _items.Remove(item);
    }

    /// <summary>
    /// Confirms the order, freezing its contents.
    /// </summary>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <exception cref="ConflictException">The order is not a draft.</exception>
    /// <exception cref="ValidationException">The order has no items or the instant is not UTC.</exception>
    public void Confirm(DateTime nowUtc)
    {
        if (Status != OrderStatus.Draft)
        {
            throw new ConflictException($"Only draft orders can be confirmed (current status: {Status}).");
        }

        if (_items.Count == 0)
        {
            throw new ValidationException("An order must contain at least one item before it can be confirmed.");
        }

        Status = OrderStatus.Confirmed;
        ConfirmedAtUtc = EnsureUtc(nowUtc);
    }

    /// <summary>
    /// Cancels the order, recording the reason.
    /// </summary>
    /// <param name="reason">Why the order was cancelled.</param>
    /// <param name="nowUtc">Current UTC instant.</param>
    /// <exception cref="ConflictException">The order is already cancelled.</exception>
    /// <exception cref="ValidationException">The reason is missing or the instant is not UTC.</exception>
    public void Cancel(string reason, DateTime nowUtc)
    {
        if (Status == OrderStatus.Cancelled)
        {
            throw new ConflictException("The order is already cancelled.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("A cancellation reason is required.");
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 500)
        {
            throw new ValidationException("The cancellation reason must be at most 500 characters.");
        }

        Status = OrderStatus.Cancelled;
        CancelledAtUtc = EnsureUtc(nowUtc);
        CancellationReason = normalizedReason;
    }

    private void EnsureDraft(string operation)
    {
        if (Status != OrderStatus.Draft)
        {
            throw new ConflictException($"Cannot {operation} because the order is {Status}.");
        }
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ValidationException("The instant must be expressed in UTC.");
        }

        return value;
    }
}
