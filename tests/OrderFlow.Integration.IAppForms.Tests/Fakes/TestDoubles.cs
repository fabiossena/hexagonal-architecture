using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Integration.IAppForms.Tests.Fakes;

/// <summary>Deterministic clock for the integration tests.</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }
}

/// <summary>In-memory <see cref="IOrderRepository"/>.</summary>
internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = new();

    public void Seed(Order order) => _orders.Add(order);

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        _orders.Add(order);
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_orders.SingleOrDefault(order => order.Id == id));

    public Task<Order?> GetByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_orders.SingleOrDefault(
            order => order.ExternalReference == externalReference));
    }

    public Task<IReadOnlyList<Order>> ListAsync(
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Order>>(_orders.Take(take).ToList());
    }

    public Task<int> CountAsync(OrderStatus? status, CancellationToken cancellationToken)
        => Task.FromResult(_orders.Count);
}

/// <summary>In-memory <see cref="IPaymentRepository"/>.</summary>
internal sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly List<Payment> _payments = new();

    public IReadOnlyList<Payment> Payments => _payments;

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        _payments.Add(payment);
        return Task.CompletedTask;
    }

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_payments.SingleOrDefault(payment => payment.Id == id));

    public Task<Payment?> GetByProviderPaymentIdAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_payments.SingleOrDefault(
            payment => payment.ProviderPaymentId == providerPaymentId));
    }

    public Task<Payment?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_payments.SingleOrDefault(
            payment => payment.IdempotencyKey == idempotencyKey));
    }

    public Task<IReadOnlyList<Payment>> ListAsync(
        Guid? orderId,
        PaymentProvider? provider,
        PaymentStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Payment>>(_payments.Take(take).ToList());
    }

    public Task<int> CountAsync(
        Guid? orderId,
        PaymentProvider? provider,
        PaymentStatus? status,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_payments.Count);
    }
}

/// <summary>Payment provider double.</summary>
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
        new("sim_1", PaymentStatus.Processing, "pending");

    public Task<ProviderChargeResult> ChargeAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
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
        return Task.FromResult(new ProviderRefundResult(true, "re_1", "succeeded"));
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

/// <summary>Resolver double.</summary>
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

/// <summary>Unit of work double.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
