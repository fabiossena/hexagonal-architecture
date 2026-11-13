using OrderFlow.Domain.Orders;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Tests.Fakes;

/// <summary>Builds aggregate instances for the use-case tests.</summary>
internal static class OrderFactory
{
    public static readonly DateTime Now = new(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
    public static readonly Guid CustomerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static Order Draft()
    {
        var order = Order.Create(CustomerId, Now);
        order.AddItem("Mechanical keyboard", 1, new Money(100m));
        return order;
    }

    public static Order Confirmed()
    {
        var order = Draft();
        order.Confirm(Now.AddMinutes(1));
        return order;
    }

    public static Order Cancelled()
    {
        var order = Draft();
        order.Cancel("Created for testing", Now.AddMinutes(1));
        return order;
    }
}
