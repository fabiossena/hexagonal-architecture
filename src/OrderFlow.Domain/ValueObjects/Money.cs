using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.ValueObjects;

/// <summary>
/// Immutable monetary amount expressed in a single ISO-4217 currency.
/// </summary>
/// <remarks>
/// Money is a value object: it has no identity, is compared by value and
/// protects its own invariants (non-negative amounts, valid currency code and
/// no cross-currency arithmetic).
/// </remarks>
public sealed record Money
{
    /// <summary>Currency used when the caller does not provide one.</summary>
    public const string DefaultCurrency = "USD";

    /// <summary>
    /// Creates a monetary amount.
    /// </summary>
    /// <param name="amount">Non-negative amount, rounded to two decimals.</param>
    /// <param name="currency">Three-letter ISO-4217 code (defaults to <see cref="DefaultCurrency"/>).</param>
    /// <exception cref="ValidationException">The amount is negative or the currency is invalid.</exception>
    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0m)
        {
            throw new ValidationException("Money amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ValidationException("Currency is required.");
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3)
        {
            throw new ValidationException("Currency must be a three-letter ISO-4217 code.");
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = normalizedCurrency;
    }

    /// <summary>Gets the amount (always non-negative, two decimals).</summary>
    public decimal Amount { get; }

    /// <summary>Gets the ISO-4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Creates a zero amount in the given currency.</summary>
    /// <param name="currency">Three-letter ISO-4217 code.</param>
    /// <returns>A zero <see cref="Money"/> instance.</returns>
    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    /// <summary>Adds two amounts of the same currency.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The sum.</returns>
    /// <exception cref="ValidationException">The currencies differ.</exception>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>Subtracts two amounts of the same currency.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="ValidationException">The currencies differ or the result would be negative.</exception>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        if (right.Amount > left.Amount)
        {
            throw new ValidationException("Money subtraction would produce a negative amount.");
        }

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>Multiplies an amount by a whole quantity.</summary>
    /// <param name="money">Amount to multiply.</param>
    /// <param name="quantity">Non-negative multiplier.</param>
    /// <returns>The product.</returns>
    /// <exception cref="ValidationException">The quantity is negative.</exception>
    public static Money operator *(Money money, int quantity)
    {
        if (quantity < 0)
        {
            throw new ValidationException("Quantity cannot be negative.");
        }

        return new Money(money.Amount * quantity, money.Currency);
    }

    /// <summary>Returns a percentage of the amount.</summary>
    /// <param name="percent">Percentage between 0 and 100.</param>
    /// <returns>The calculated part.</returns>
    /// <exception cref="ValidationException">The percentage is outside 0..100.</exception>
    public Money Percentage(decimal percent)
    {
        if (percent is < 0m or > 100m)
        {
            throw new ValidationException("Percentage must be between 0 and 100.");
        }

        return new Money(Amount * percent / 100m, Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new ValidationException(
                $"Cannot combine amounts in different currencies ({left.Currency} and {right.Currency}).");
        }
    }
}
