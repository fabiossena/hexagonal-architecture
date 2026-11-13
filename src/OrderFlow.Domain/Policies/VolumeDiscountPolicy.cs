using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Policies;

/// <summary>
/// Volume-discount policy applied to an order.
/// </summary>
/// <remarks>
/// A policy (a.k.a. specification) keeps a pricing rule out of the aggregate
/// while still being part of the domain: it has no dependencies, is pure and is
/// trivially unit-testable.
/// <para>Rules, evaluated in order:</para>
/// <list type="number">
///   <item><description>at least 10 items and a subtotal of 5000 → 10% discount;</description></item>
///   <item><description>at least 3 items and a subtotal of 1000 → 5% discount;</description></item>
///   <item><description>otherwise → no discount.</description></item>
/// </list>
/// </remarks>
public static class VolumeDiscountPolicy
{
    /// <summary>Minimum number of items for the bulk discount.</summary>
    public const int BulkItemThreshold = 3;

    /// <summary>Minimum number of items for the large-order discount.</summary>
    public const int LargeOrderItemThreshold = 10;

    /// <summary>Minimum subtotal for the bulk discount.</summary>
    public const decimal BulkSubtotalThreshold = 1000m;

    /// <summary>Minimum subtotal for the large-order discount.</summary>
    public const decimal LargeOrderSubtotalThreshold = 5000m;

    /// <summary>Bulk discount percentage.</summary>
    public const decimal BulkDiscountPercent = 5m;

    /// <summary>Large-order discount percentage.</summary>
    public const decimal LargeOrderDiscountPercent = 10m;

    /// <summary>
    /// Calculates the discount for a subtotal and item count.
    /// </summary>
    /// <param name="subtotal">Sum of the order lines.</param>
    /// <param name="itemCount">Number of lines in the order.</param>
    /// <returns>The discount amount (zero when no rule applies).</returns>
    /// <exception cref="ValidationException">The item count is negative.</exception>
    public static Money Calculate(Money subtotal, int itemCount)
    {
        ArgumentNullException.ThrowIfNull(subtotal);

        if (itemCount < 0)
        {
            throw new ValidationException("Item count cannot be negative.");
        }

        if (itemCount >= LargeOrderItemThreshold && subtotal.Amount >= LargeOrderSubtotalThreshold)
        {
            return subtotal.Percentage(LargeOrderDiscountPercent);
        }

        if (itemCount >= BulkItemThreshold && subtotal.Amount >= BulkSubtotalThreshold)
        {
            return subtotal.Percentage(BulkDiscountPercent);
        }

        return Money.Zero(subtotal.Currency);
    }
}
