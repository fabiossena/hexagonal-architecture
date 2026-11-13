using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Adapters.Outbound.Payments;

/// <summary>
/// Default implementation of <see cref="IPaymentProviderResolver"/>: it keeps
/// every registered provider adapter and hands the right one on request.
/// </summary>
/// <remarks>
/// This is the only piece that knows *which* providers exist — the use cases ask
/// for a provider by identity and never enumerate the implementations.
/// </remarks>
public sealed class PaymentProviderResolver : IPaymentProviderResolver
{
    private readonly IReadOnlyList<IPaymentProvider> _providers;

    /// <summary>Creates the resolver.</summary>
    /// <param name="providers">Registered provider adapters.</param>
    public PaymentProviderResolver(IEnumerable<IPaymentProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PaymentProviderDescriptor> Describe()
    {
        return _providers
            .Select(provider => new PaymentProviderDescriptor(
                provider.Provider,
                provider.DisplayName,
                provider.SupportedMethods,
                provider.IsConfigured,
                provider.IsSimulation))
            .ToList();
    }

    /// <inheritdoc />
    public IPaymentProvider Resolve(PaymentProvider provider)
    {
        var adapter = _providers.FirstOrDefault(candidate => candidate.Provider == provider);

        if (adapter is null)
        {
            throw new ValidationException($"Payment provider '{provider}' is not registered.");
        }

        return adapter;
    }
}
