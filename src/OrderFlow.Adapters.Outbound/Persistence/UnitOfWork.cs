using OrderFlow.Application.Abstractions;

namespace OrderFlow.Adapters.Outbound.Persistence;

/// <summary>
/// EF Core implementation of the <see cref="IUnitOfWork"/> outbound port.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly OrderFlowDbContext _dbContext;

    /// <summary>Creates the unit of work.</summary>
    /// <param name="dbContext">Scoped DbContext.</param>
    public UnitOfWork(OrderFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
