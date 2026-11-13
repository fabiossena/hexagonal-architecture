namespace OrderFlow.Domain.Orders;

/// <summary>Lifecycle of an order.</summary>
public enum OrderStatus
{
    /// <summary>Order is being assembled; items can still be added or removed.</summary>
    Draft = 0,

    /// <summary>Order is confirmed and can no longer be modified (only cancelled).</summary>
    Confirmed = 1,

    /// <summary>Order was cancelled; the reason is recorded on the aggregate.</summary>
    Cancelled = 2
}
