using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Application.Exceptions;

/// <summary>
/// Raised when a payment does not exist (maps to HTTP 404 in the inbound adapter).
/// </summary>
public sealed class PaymentNotFoundException : DomainException
{
    /// <summary>Creates the exception for a missing payment.</summary>
    /// <param name="paymentId">Identity that was not found.</param>
    public PaymentNotFoundException(Guid paymentId)
        : base($"Payment '{paymentId}' was not found.")
    {
        PaymentId = paymentId;
    }

    /// <summary>Gets the identity that was not found.</summary>
    public Guid PaymentId { get; }
}
