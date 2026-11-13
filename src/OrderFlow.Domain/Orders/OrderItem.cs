using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Orders;

/// <summary>
/// A single line of an order. Items are entities (they have identity) but are
/// only reachable through the <see cref="Order"/> aggregate root.
/// </summary>
public sealed class OrderItem
{
    /// <summary>Maximum length accepted for a product name.</summary>
    public const int MaxProductNameLength = 200;

    /// <summary>Maximum quantity accepted per line.</summary>
    public const int MaxQuantity = 1000;

    // Required by EF Core materialization.
    private OrderItem()
    {
    }

    internal OrderItem(Guid id, string productName, int quantity, Money unitPrice)
    {
        Id = id;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>Gets the item identity.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the product name captured when the item was added.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Gets the quantity ordered.</summary>
    public int Quantity { get; private set; }

    /// <summary>Gets the unit price.</summary>
    public Money UnitPrice { get; private set; } = Money.Zero();

    /// <summary>Gets the line total (<see cref="UnitPrice"/> × <see cref="Quantity"/>).</summary>
    public Money LineTotal => UnitPrice * Quantity;

    internal static OrderItem Create(string productName, int quantity, Money unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ValidationException("Product name is required.");
        }

        var normalizedName = productName.Trim();
        if (normalizedName.Length > MaxProductNameLength)
        {
            throw new ValidationException(
                $"Product name must be at most {MaxProductNameLength} characters.");
        }

        if (quantity is < 1 or > MaxQuantity)
        {
            throw new ValidationException($"Quantity must be between 1 and {MaxQuantity}.");
        }

        ArgumentNullException.ThrowIfNull(unitPrice);

        return new OrderItem(Guid.NewGuid(), normalizedName, quantity, unitPrice);
    }
}
