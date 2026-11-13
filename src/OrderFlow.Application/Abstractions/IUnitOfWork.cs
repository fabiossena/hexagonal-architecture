namespace OrderFlow.Application.Abstractions;

/// <summary>
/// Outbound port that commits the pending changes of a unit of work.
/// </summary>
/// <remarks>
/// Keeping this separate from <see cref="IOrderRepository"/> mirrors the real
/// transactional boundary (EF Core's <c>SaveChangesAsync</c>) without leaking
/// EF types into the application layer.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>Commits all pending changes.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
