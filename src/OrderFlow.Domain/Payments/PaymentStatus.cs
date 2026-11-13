namespace OrderFlow.Domain.Payments;

/// <summary>Lifecycle of a payment attempt.</summary>
public enum PaymentStatus
{
    /// <summary>Created locally, not yet sent to the provider.</summary>
    Pending = 0,

    /// <summary>The provider accepted the charge and is still processing it.</summary>
    Processing = 1,

    /// <summary>The money was captured by the provider.</summary>
    Paid = 2,

    /// <summary>The provider rejected the charge (or the call failed).</summary>
    Failed = 3,

    /// <summary>The payment was cancelled before being captured.</summary>
    Cancelled = 4,

    /// <summary>The payment was captured and later refunded.</summary>
    Refunded = 5
}
