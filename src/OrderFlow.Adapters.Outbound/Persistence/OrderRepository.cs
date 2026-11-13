using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Adapters.Outbound.Persistence;

/// <summary>
/// EF Core implementation of the <see cref="IOrderRepository"/> outbound port.
/// </summary>
public sealed class OrderRepository : IOrderRepository
{
    private readonly OrderFlowDbContext _dbContext;

    /// <summary>Creates the repository.</summary>
    /// <param name="dbContext">Scoped DbContext.</param>
    public OrderRepository(OrderFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Orders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Order?> GetByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return null;
        }

        var normalized = externalReference.Trim();

        return await _dbContext.Orders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.ExternalReference == normalized, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> ListAsync(
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        IQueryable<Order> query = _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items);

        if (status is { } value)
        {
            query = query.Where(order => order.Status == value);
        }

        return await query
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(OrderStatus? status, CancellationToken cancellationToken)
    {
        IQueryable<Order> query = _dbContext.Orders;

        if (status is { } value)
        {
            query = query.Where(order => order.Status == value);
        }

        return await query.CountAsync(cancellationToken);
    }
}
