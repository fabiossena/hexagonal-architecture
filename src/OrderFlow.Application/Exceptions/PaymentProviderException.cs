namespace OrderFlow.Application.Exceptions;

/// <summary>
/// Raised when a payment provider cannot be reached or answers with an error
/// (maps to HTTP 502 in the inbound adapter).
/// </summary>
/// <remarks>
/// This is an infrastructure failure, not a broken business rule, so it does not
/// derive from <see cref="OrderFlow.Domain.Exceptions.DomainException"/>.
/// </remarks>
public sealed class PaymentProviderException : Exception
{
    /// <summary>Creates the exception with a human-readable message.</summary>
    /// <param name="message">Description of the failure.</param>
    public PaymentProviderException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a human-readable message and the provider payload.</summary>
    /// <param name="message">Description of the failure.</param>
    /// <param name="providerPayload">Raw payload returned by the provider, when available.</param>
    public PaymentProviderException(string message, string? providerPayload)
        : base(message)
    {
        ProviderPayload = providerPayload;
    }

    /// <summary>Creates the exception with a human-readable message and the original cause.</summary>
    /// <param name="message">Description of the failure.</param>
    /// <param name="innerException">Original exception.</param>
    public PaymentProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the raw payload returned by the provider, when available.</summary>
    public string? ProviderPayload { get; }
}
