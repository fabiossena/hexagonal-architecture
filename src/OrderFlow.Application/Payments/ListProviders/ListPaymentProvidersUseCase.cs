using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;

namespace OrderFlow.Application.Payments.ListProviders;

/// <summary>
/// Use case: list the available payment providers.
/// </summary>
/// <remarks>
/// The test screen renders this catalogue to decide which provider and which
/// payment instruments to offer. It never exposes credentials.
/// </remarks>
public sealed class ListPaymentProvidersUseCase
{
    private readonly IPaymentProviderResolver _providers;

    /// <summary>Creates the use case.</summary>
    /// <param name="providers">Provider resolver port.</param>
    public ListPaymentProvidersUseCase(IPaymentProviderResolver providers)
    {
        _providers = providers;
    }

    /// <summary>Returns the provider catalogue.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per registered provider.</returns>
    public Task<IReadOnlyCollection<PaymentProviderDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var descriptors = _providers.Describe();

        IReadOnlyCollection<PaymentProviderDto> result = descriptors
            .Select(PaymentProviderDto.FromDescriptor)
            .OrderBy(provider => provider.Provider, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(result);
    }
}
