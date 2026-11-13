using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;
using Xunit;

namespace OrderFlow.Domain.Tests;

/// <summary>Boundary behaviour of <see cref="VolumeDiscountPolicy"/>.</summary>
public sealed class VolumeDiscountPolicyTests
{
    [Fact]
    public void Calculate_BelowThresholds_ReturnsZero()
    {
        var discount = VolumeDiscountPolicy.Calculate(new Money(999.99m), itemCount: 2);

        Assert.Equal(0m, discount.Amount);
    }

    [Fact]
    public void Calculate_ExactlyAtBulkThreshold_AppliesFivePercent()
    {
        var discount = VolumeDiscountPolicy.Calculate(new Money(1000m), itemCount: 3);

        Assert.Equal(50m, discount.Amount);
    }

    [Fact]
    public void Calculate_AtBulkAmountButTooFewItems_ReturnsZero()
    {
        var discount = VolumeDiscountPolicy.Calculate(new Money(2000m), itemCount: 2);

        Assert.Equal(0m, discount.Amount);
    }

    [Fact]
    public void Calculate_AtLargeOrderThreshold_AppliesTenPercent()
    {
        var discount = VolumeDiscountPolicy.Calculate(new Money(5000m), itemCount: 10);

        Assert.Equal(500m, discount.Amount);
    }

    [Fact]
    public void Calculate_AboveLargeThreshold_AppliesTenPercent()
    {
        var discount = VolumeDiscountPolicy.Calculate(new Money(9000m), itemCount: 12);

        Assert.Equal(900m, discount.Amount);
    }

    [Fact]
    public void Calculate_NegativeItemCount_Throws()
    {
        Assert.Throws<ValidationException>(() =>
            VolumeDiscountPolicy.Calculate(new Money(100m), itemCount: -1));
    }

    [Fact]
    public void Calculate_KeepsCurrency()
    {
        var discount = VolumeDiscountPolicy.Calculate(new Money(1000m, "BRL"), itemCount: 3);

        Assert.Equal("BRL", discount.Currency);
    }
}
