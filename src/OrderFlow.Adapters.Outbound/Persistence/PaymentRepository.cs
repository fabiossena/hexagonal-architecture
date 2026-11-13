using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Adapters.Outbound.Persistence;

/// <summary>
/// EF Core implementation of the <see cref="IPaymentRepository"/> outbound port.
/// </summary>
public sealed class PaymentRepository : IPaymentRepository
{
    private readonly OrderFlowDbContext _dbContext;

    /// <summary>Creates the repository.</summary>
    /// <param name="dbContext">Scoped DbContext.</param>
    public PaymentRepository(OrderFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await _dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Payments
            .FirstOrDefaultAsync(payment => payment.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Payment?> GetByProviderPaymentIdAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Payments
            .FirstOrDefaultAsync(
                payment => payment.ProviderPaymentId == providerPaymentId,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Payment>> ListAsync(
        Guid? orderId,
        PaymentProvider? provider,
        PaymentStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(_dbContext.Payments.AsNoTracking(), orderId, provider, status);

        return await query
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(
        Guid? orderId,
        PaymentProvider? provider,
        PaymentStatus? status,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(_dbContext.Payments, orderId, provider, status);

        return await query.CountAsync(cancellationToken);
    }

    private static IQueryable<Payment> ApplyFilters(
        IQueryable<Payment> query,
        Guid? orderId,
        PaymentProvider? provider,
        PaymentStatus? status)
    {
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

        return query;
    }
}
