using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Adapters.Outbound.Persistence;

/// <summary>
/// EF Core unit of work for the OrderFlow bounded context.
/// </summary>
/// <remarks>
/// The DbContext lives in the outbound adapter: the domain and the application
/// layers never see it.
/// </remarks>
public sealed class OrderFlowDbContext : DbContext
{
    /// <summary>Creates the context.</summary>
    /// <param name="options">EF Core options (provider + connection string).</param>
    public OrderFlowDbContext(DbContextOptions<OrderFlowDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the orders aggregate set.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Gets the payments aggregate set.</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderFlowDbContext).Assembly);
    }
}
