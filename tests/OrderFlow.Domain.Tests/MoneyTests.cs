using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.ValueObjects;
using Xunit;

namespace OrderFlow.Domain.Tests;

/// <summary>Invariants and arithmetic of the <see cref="Money"/> value object.</summary>
public sealed class MoneyTests
{
    [Fact]
    public void Constructor_NegativeAmount_Throws()
    {
        Assert.Throws<ValidationException>(() => new Money(-0.01m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Constructor_InvalidCurrency_Throws(string currency)
    {
        Assert.Throws<ValidationException>(() => new Money(10m, currency));
    }

    [Fact]
    public void Constructor_NormalizesCurrencyAndRoundsAmount()
    {
        var money = new Money(10.005m, "usd");

        Assert.Equal("USD", money.Currency);
        Assert.Equal(10.01m, money.Amount);
    }

    [Fact]
    public void Addition_WithSameCurrency_ReturnsSum()
    {
        var result = new Money(10.10m, "USD") + new Money(5.20m, "USD");

        Assert.Equal(15.30m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Addition_WithDifferentCurrencies_Throws()
    {
        Assert.Throws<ValidationException>(() => new Money(10m, "USD") + new Money(10m, "EUR"));
    }

    [Fact]
    public void Subtraction_ReturnsDifference()
    {
        var result = new Money(20m, "USD") - new Money(5m, "USD");

        Assert.Equal(15m, result.Amount);
    }

    [Fact]
    public void Subtraction_BelowZero_Throws()
    {
        Assert.Throws<ValidationException>(() => new Money(5m, "USD") - new Money(10m, "USD"));
    }

    [Fact]
    public void Multiplication_ByQuantity_ReturnsProduct()
    {
        var result = new Money(12.50m, "USD") * 4;

        Assert.Equal(50m, result.Amount);
    }

    [Fact]
    public void Multiplication_ByNegativeQuantity_Throws()
    {
        Assert.Throws<ValidationException>(() => new Money(10m, "USD") * -1);
    }

    [Fact]
    public void Percentage_ReturnsPartOfAmount()
    {
        var discount = new Money(200m, "USD").Percentage(5m);

        Assert.Equal(10m, discount.Amount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Percentage_OutsideRange_Throws(decimal percent)
    {
        Assert.Throws<ValidationException>(() => new Money(100m, "USD").Percentage(percent));
    }

    [Fact]
    public void Zero_UsesProvidedCurrency()
    {
        var zero = Money.Zero("BRL");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("BRL", zero.Currency);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(new Money(10m, "USD"), new Money(10m, "USD"));
        Assert.NotEqual(new Money(10m, "USD"), new Money(10m, "EUR"));
    }
}
