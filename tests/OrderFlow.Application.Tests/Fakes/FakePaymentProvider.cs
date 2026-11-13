using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Tests.Fakes;

/// <summary>Payment provider double: it answers whatever the test configures.</summary>
internal sealed class FakePaymentProvider : IPaymentProvider
{
    public FakePaymentProvider(PaymentProvider provider = PaymentProvider.Simulated, bool isConfigured = true)
    {
        Provider = provider;
        IsConfigured = isConfigured;
    }

    public PaymentProvider Provider { get; }

    public string DisplayName => Provider.ToString();

    public bool IsSimulation => Provider == PaymentProvider.Simulated;

    public bool IsConfigured { get; }

    public IReadOnlyCollection<PaymentMethod> SupportedMethods { get; set; } =
        PaymentPolicy.MethodsFor(PaymentProvider.Simulated);

    public ProviderChargeResult ChargeResult { get; set; } =
        new("sim_1", PaymentStatus.Paid, "approved");

    public Exception? ChargeException { get; set; }

    public ProviderRefundResult RefundResult { get; set; } =
        new(true, "re_1", "succeeded");

    public ProviderChargeRequest? LastRequest { get; private set; }

    public Task<ProviderChargeResult> ChargeAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        if (ChargeException is not null)
        {
            throw ChargeException;
        }

        return Task.FromResult(ChargeResult);
    }

    public Task<ProviderChargeResult> GetChargeAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ChargeResult with { ProviderPaymentId = providerPaymentId });
    }

    public Task<ProviderRefundResult> RefundAsync(
        string providerPaymentId,
        Money amount,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(RefundResult);
    }

    public PaymentStatus MapStatus(string providerStatus)
    {
        return providerStatus.ToLowerInvariant() switch
        {
            "approved" or "paid" or "succeeded" => PaymentStatus.Paid,
            "pending" or "processing" => PaymentStatus.Processing,
            "refunded" => PaymentStatus.Refunded,
            _ => PaymentStatus.Failed
        };
    }
}

/// <summary>Resolver double that hands out the configured providers.</summary>
internal sealed class FakePaymentProviderResolver : IPaymentProviderResolver
{
    private readonly List<IPaymentProvider> _providers;

    public FakePaymentProviderResolver(params IPaymentProvider[] providers)
    {
        _providers = providers.ToList();
    }

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

    public IPaymentProvider Resolve(PaymentProvider provider)
    {
        return _providers.FirstOrDefault(candidate => candidate.Provider == provider)
            ?? throw new ValidationException($"Payment provider '{provider}' is not registered.");
    }
}
