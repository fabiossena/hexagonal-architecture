using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Adapters.Outbound.Persistence.Configurations;

/// <summary>
/// Relational mapping of the <see cref="Order"/> aggregate.
/// </summary>
/// <remarks>
/// The aggregate maps to <c>orders</c> plus an owned <c>order_items</c> table.
/// Computed properties (<c>Subtotal</c>, <c>Discount</c>, <c>Total</c>,
/// <c>LineTotal</c>) are not persisted; they are derived from the stored data.
/// </remarks>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.CustomerId).IsRequired();
        builder.Property(order => order.ExternalReference).HasMaxLength(Order.MaxExternalReferenceLength);
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(order => order.CreatedAtUtc).IsRequired();
        builder.Property(order => order.CancellationReason).HasMaxLength(500);

        builder.Ignore(order => order.Subtotal);
        builder.Ignore(order => order.Discount);
        builder.Ignore(order => order.Total);

        builder.OwnsMany(order => order.Items, items =>
        {
            items.ToTable("order_items");
            items.WithOwner().HasForeignKey("order_id");
            items.Property(item => item.Id).ValueGeneratedNever();
            items.HasKey(item => item.Id);
            items.Property(item => item.ProductName)
                .HasMaxLength(OrderItem.MaxProductNameLength)
                .IsRequired();
            items.Property(item => item.Quantity).IsRequired();
            items.Ignore(item => item.LineTotal);

            items.OwnsOne(item => item.UnitPrice, money =>
            {
                money.Property(value => value.Amount)
                    .HasColumnName("unit_price")
                    .HasPrecision(18, 2)
                    .IsRequired();
                money.Property(value => value.Currency)
                    .HasColumnName("currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            items.Navigation(item => item.UnitPrice).IsRequired();
        });

        builder.HasIndex(order => order.ExternalReference);
    }
}
