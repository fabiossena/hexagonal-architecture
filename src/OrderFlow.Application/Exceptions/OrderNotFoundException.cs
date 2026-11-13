using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Application.Exceptions;

/// <summary>
/// Raised when an order does not exist (maps to HTTP 404 in the inbound adapter).
/// </summary>
public sealed class OrderNotFoundException : DomainException
{
    /// <summary>Creates the exception for a missing order.</summary>
    /// <param name="orderId">Identity that was not found.</param>
    public OrderNotFoundException(Guid orderId)
        : base($"Order '{orderId}' was not found.")
    {
        OrderId = orderId;
    }

    /// <summary>Gets the identity that was not found.</summary>
    public Guid OrderId { get; }
}
