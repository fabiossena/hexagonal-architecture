using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Application.Dtos;

/// <summary>Read model of a provider, used by the test screen.</summary>
/// <param name="Provider">Provider identity.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Methods">Accepted payment instruments.</param>
/// <param name="IsConfigured">Whether the provider has the credentials it needs.</param>
/// <param name="IsSimulation">Whether the provider is the offline simulator.</param>
public sealed record PaymentProviderDto(
    string Provider,
    string DisplayName,
    IReadOnlyCollection<string> Methods,
    bool IsConfigured,
    bool IsSimulation)
{
    /// <summary>Creates the read model from the provider descriptor.</summary>
    /// <param name="descriptor">Descriptor returned by the resolver port.</param>
    /// <returns>The read model.</returns>
    public static PaymentProviderDto FromDescriptor(PaymentProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new PaymentProviderDto(
            descriptor.Provider.ToString(),
            descriptor.DisplayName,
            descriptor.Methods.Select(method => method.ToString()).ToList(),
            descriptor.IsConfigured,
            descriptor.IsSimulation);
    }
}
