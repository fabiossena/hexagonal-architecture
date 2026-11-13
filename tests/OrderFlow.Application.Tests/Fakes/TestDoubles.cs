using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Tests.Fakes;

/// <summary>Deterministic clock for use-case tests.</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }
}

/// <summary>In-memory <see cref="IOrderRepository"/> (no database required).</summary>
internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = new();

    public IReadOnlyList<Order> Orders => _orders;

    /// <summary>Pre-loads the repository with an order.</summary>
    public void Seed(Order order) => _orders.Add(order);

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        _orders.Add(order);
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_orders.SingleOrDefault(order => order.Id == id));

    public Task<IReadOnlyList<Order>> ListAsync(
        OrderStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        IEnumerable<Order> query = _orders;
        if (status is { } value)
        {
            query = query.Where(order => order.Status == value);
        }

        IReadOnlyList<Order> page = query
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(page);
    }

    public Task<int> CountAsync(OrderStatus? status, CancellationToken cancellationToken)
    {
        IEnumerable<Order> query = _orders;
        if (status is { } value)
        {
            query = query.Where(order => order.Status == value);
        }

        return Task.FromResult(query.Count());
    }
}

/// <summary>Counts how many times the use case committed the unit of work.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
