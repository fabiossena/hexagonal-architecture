using OrderFlow.Domain.Payments;

namespace OrderFlow.Application.Abstractions;

/// <summary>
/// Outbound port that resolves the provider adapter to be used for a charge.
/// </summary>
/// <remarks>
/// The application asks for a provider by identity and for the catalogue of
/// available providers (the test screen renders it). The resolver is implemented
/// by the outbound adapter that owns the concrete provider clients.
/// </remarks>
public interface IPaymentProviderResolver
{
    /// <summary>Describes every registered provider.</summary>
    /// <returns>The provider catalogue.</returns>
    IReadOnlyCollection<PaymentProviderDescriptor> Describe();

    /// <summary>Resolves the adapter of a provider.</summary>
    /// <param name="provider">Provider identity.</param>
    /// <returns>The provider adapter.</returns>
    /// <exception cref="Domain.Exceptions.ValidationException">The provider is not registered.</exception>
    IPaymentProvider Resolve(PaymentProvider provider);
}
