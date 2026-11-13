using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Adapters.Outbound.Persistence.Configurations;

/// <summary>
/// Relational mapping of the <see cref="Payment"/> aggregate.
/// </summary>
/// <remarks>
/// The aggregate maps to the <c>payments</c> table. The amount is stored as an
/// owned <c>Money</c> (amount + currency) and the enum values are stored as
/// readable strings.
/// </remarks>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.OrderId).IsRequired();
        builder.Property(payment => payment.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.Description)
            .HasMaxLength(Payment.MaxDescriptionLength)
            .IsRequired();
        builder.Property(payment => payment.CustomerEmail).HasMaxLength(Payment.MaxCustomerEmailLength);
        builder.Property(payment => payment.ProviderPaymentId).HasMaxLength(Payment.MaxProviderReferenceLength);
        builder.Property(payment => payment.ProviderStatus).HasMaxLength(Payment.MaxProviderReferenceLength);
        builder.Property(payment => payment.CheckoutUrl).HasMaxLength(1000);
        builder.Property(payment => payment.PixCopyPasteCode).HasMaxLength(1000);

        // The QR code is a base64 image: it needs an unbounded column.
        builder.Property(payment => payment.PixQrCodeBase64).HasColumnType("text");
        builder.Property(payment => payment.FailureReason).HasMaxLength(1000);
        builder.Property(payment => payment.ProviderPayload).HasColumnType("text");
        builder.Property(payment => payment.RefundedAmountValue).HasPrecision(18, 2).IsRequired();
        builder.Property(payment => payment.CreatedAtUtc).IsRequired();
        builder.Property(payment => payment.UpdatedAtUtc).IsRequired();

        builder.OwnsOne(payment => payment.Amount, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(value => value.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Navigation(payment => payment.Amount).IsRequired();

        builder.Ignore(payment => payment.RefundedAmount);
        builder.Ignore(payment => payment.RefundableAmount);
        builder.Ignore(payment => payment.IsTerminal);

        builder.HasIndex(payment => payment.OrderId);
        builder.HasIndex(payment => payment.ProviderPaymentId);
    }
}
