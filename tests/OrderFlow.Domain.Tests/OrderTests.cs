using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.ValueObjects;
using Xunit;

namespace OrderFlow.Domain.Tests;

/// <summary>
/// Invariants of the <see cref="Order"/> aggregate.
/// </summary>
public sealed class OrderTests
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithEmptyCustomer_Throws()
    {
        Assert.Throws<ValidationException>(() => Order.Create(Guid.Empty, Now));
    }

    [Fact]
    public void Create_WithNonUtcInstant_Throws()
    {
        var local = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local);
        Assert.Throws<ValidationException>(() => Order.Create(CustomerId, local));
    }

    [Fact]
    public void Create_StartsAsDraftWithoutItems()
    {
        var order = Order.Create(CustomerId, Now);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(CustomerId, order.CustomerId);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Empty(order.Items);
        Assert.Equal(Now, order.CreatedAtUtc);
        Assert.Null(order.ConfirmedAtUtc);
        Assert.Null(order.CancelledAtUtc);
    }

    [Fact]
    public void AddItem_ToDraft_AddsLineAndComputesTotals()
    {
        var order = Order.Create(CustomerId, Now);

        order.AddItem("Mechanical keyboard", 2, new Money(50m));

        var item = Assert.Single(order.Items);
        Assert.Equal("Mechanical keyboard", item.ProductName);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(100m, item.LineTotal.Amount);
        Assert.Equal(100m, order.Subtotal.Amount);
        Assert.Equal(0m, order.Discount.Amount);
        Assert.Equal(100m, order.Total.Amount);
    }

    [Fact]
    public void AddItem_AfterConfirmation_ThrowsConflict()
    {
        var order = ConfirmedOrder();

        Assert.Throws<ConflictException>(() => order.AddItem("Mouse", 1, new Money(10m)));
    }

    [Fact]
    public void AddItem_WithDifferentCurrency_Throws()
    {
        var order = Order.Create(CustomerId, Now);
        order.AddItem("Keyboard", 1, new Money(10m, "USD"));

        Assert.Throws<ValidationException>(() => order.AddItem("Mouse", 1, new Money(10m, "EUR")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void AddItem_WithInvalidQuantity_Throws(int quantity)
    {
        var order = Order.Create(CustomerId, Now);

        Assert.Throws<ValidationException>(() => order.AddItem("Keyboard", quantity, new Money(10m)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddItem_WithBlankProductName_Throws(string productName)
    {
        var order = Order.Create(CustomerId, Now);

        Assert.Throws<ValidationException>(() => order.AddItem(productName, 1, new Money(10m)));
    }

    [Fact]
    public void RemoveItem_RemovesFromDraft()
    {
        var order = Order.Create(CustomerId, Now);
        order.AddItem("Keyboard", 1, new Money(10m));
        var itemId = order.Items[0].Id;

        order.RemoveItem(itemId);

        Assert.Empty(order.Items);
    }

    [Fact]
    public void RemoveItem_UnknownItem_Throws()
    {
        var order = Order.Create(CustomerId, Now);

        Assert.Throws<ValidationException>(() => order.RemoveItem(Guid.NewGuid()));
    }

    [Fact]
    public void Confirm_WithoutItems_Throws()
    {
        var order = Order.Create(CustomerId, Now);

        Assert.Throws<ValidationException>(() => order.Confirm(Now));
    }

    [Fact]
    public void Confirm_WhenDraft_SetsStatusAndTimestamp()
    {
        var order = Order.Create(CustomerId, Now);
        order.AddItem("Keyboard", 1, new Money(10m));
        var confirmedAt = Now.AddMinutes(5);

        order.Confirm(confirmedAt);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(confirmedAt, order.ConfirmedAtUtc);
    }

    [Fact]
    public void Confirm_Twice_ThrowsConflict()
    {
        var order = ConfirmedOrder();

        Assert.Throws<ConflictException>(() => order.Confirm(Now.AddMinutes(5)));
    }

    [Fact]
    public void Cancel_Draft_RecordsReason()
    {
        var order = Order.Create(CustomerId, Now);
        var cancelledAt = Now.AddMinutes(2);

        order.Cancel("Customer changed their mind", cancelledAt);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Customer changed their mind", order.CancellationReason);
        Assert.Equal(cancelledAt, order.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_Confirmed_IsAllowed()
    {
        var order = ConfirmedOrder();

        order.Cancel("Out of stock", Now.AddMinutes(10));

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Cancel_WithoutReason_Throws(string reason)
    {
        var order = Order.Create(CustomerId, Now);

        Assert.Throws<ValidationException>(() => order.Cancel(reason, Now));
    }

    [Fact]
    public void Cancel_Twice_ThrowsConflict()
    {
        var order = Order.Create(CustomerId, Now);
        order.Cancel("First", Now);

        Assert.Throws<ConflictException>(() => order.Cancel("Second", Now.AddMinutes(1)));
    }

    [Fact]
    public void Total_AppliesBulkVolumeDiscount()
    {
        var order = Order.Create(CustomerId, Now);
        for (var index = 0; index < 3; index++)
        {
            order.AddItem($"Item {index}", 1, new Money(400m));
        }

        Assert.Equal(1200m, order.Subtotal.Amount);
        Assert.Equal(60m, order.Discount.Amount); // 5% of 1200
        Assert.Equal(1140m, order.Total.Amount);
    }

    [Fact]
    public void Total_AppliesLargeOrderDiscount()
    {
        var order = Order.Create(CustomerId, Now);
        for (var index = 0; index < 10; index++)
        {
            order.AddItem($"Item {index}", 1, new Money(600m));
        }

        Assert.Equal(6000m, order.Subtotal.Amount);
        Assert.Equal(600m, order.Discount.Amount); // 10% of 6000
        Assert.Equal(5400m, order.Total.Amount);
    }

    private static Order ConfirmedOrder()
    {
        var order = Order.Create(CustomerId, Now);
        order.AddItem("Keyboard", 1, new Money(10m));
        order.Confirm(Now);
        return order;
    }
}
