using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IPaymentRepository"/> (no database required).</summary>
internal sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly List<Payment> _payments = new();

    public IReadOnlyList<Payment> Payments => _payments;

    /// <summary>Pre-loads the repository with a payment.</summary>
    public void Seed(Payment payment) => _payments.Add(payment);

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
        IEnumerable<Payment> query = _payments;

        if (orderId is { } order)
        {
            query = query.Where(payment => payment.OrderId == order);
        }

        if (provider is { } providerValue)
        {
            query = query.Where(payment => payment.Provider == providerValue);
        }

        if (status is { } statusValue)
        {
            query = query.Where(payment => payment.Status == statusValue);
        }

        IReadOnlyList<Payment> page = query
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(page);
    }

    public Task<int> CountAsync(
        Guid? orderId,
        PaymentProvider? provider,
        PaymentStatus? status,
        CancellationToken cancellationToken)
    {
        IEnumerable<Payment> query = _payments;

        if (orderId is { } order)
        {
            query = query.Where(payment => payment.OrderId == order);
        }

        if (provider is { } providerValue)
        {
            query = query.Where(payment => payment.Provider == providerValue);
        }

        if (status is { } statusValue)
        {
            query = query.Where(payment => payment.Status == statusValue);
        }

        return Task.FromResult(query.Count());
    }
}
