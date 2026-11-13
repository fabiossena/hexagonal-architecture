using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.ValueObjects;
using Xunit;

namespace OrderFlow.Domain.Tests;

/// <summary>Invariants of the <see cref="Payment"/> aggregate.</summary>
public sealed class PaymentTests
{
    private static readonly DateTime Now = new(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OrderId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static Payment PendingPayment(PaymentProvider provider = PaymentProvider.Simulated)
    {
        return Payment.Create(
            OrderId,
            provider,
            PaymentMethod.Pix,
            new Money(100m, "BRL"),
            "Order 5555",
            "buyer@example.com",
            Now);
    }

    [Fact]
    public void Create_WithValidData_ReturnsPendingPayment()
    {
        var payment = PendingPayment();

        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(OrderId, payment.OrderId);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentProvider.Simulated, payment.Provider);
        Assert.Equal(PaymentMethod.Pix, payment.Method);
        Assert.Equal(100m, payment.Amount.Amount);
        Assert.Equal("BRL", payment.Amount.Currency);
        Assert.Equal(Now, payment.CreatedAtUtc);
        Assert.False(payment.IsTerminal);
    }

    [Fact]
    public void Create_WithZeroAmount_Throws()
    {
        Assert.Throws<ValidationException>(() => Payment.Create(
            OrderId,
            PaymentProvider.Simulated,
            PaymentMethod.Pix,
            new Money(0m, "BRL"),
            "Order 5555",
            null,
            Now));
    }

    [Fact]
    public void Create_WithUnsupportedMethod_Throws()
    {
        Assert.Throws<ValidationException>(() => Payment.Create(
            OrderId,
            PaymentProvider.Stripe,
            PaymentMethod.BankTransfer,
            new Money(10m, "BRL"),
            "Order 5555",
            null,
            Now));
    }

    [Fact]
    public void Create_WithoutOrderId_Throws()
    {
        Assert.Throws<ValidationException>(() => Payment.Create(
            Guid.Empty,
            PaymentProvider.Simulated,
            PaymentMethod.Pix,
            new Money(10m, "BRL"),
            "Order 5555",
            null,
            Now));
    }

    [Fact]
    public void Create_WithoutDescription_Throws()
    {
        Assert.Throws<ValidationException>(() => Payment.Create(
            OrderId,
            PaymentProvider.Simulated,
            PaymentMethod.Pix,
            new Money(10m, "BRL"),
            "   ",
            null,
            Now));
    }

    [Fact]
    public void Create_WithLocalInstant_Throws()
    {
        Assert.Throws<ValidationException>(() => Payment.Create(
            OrderId,
            PaymentProvider.Simulated,
            PaymentMethod.Pix,
            new Money(10m, "BRL"),
            "Order 5555",
            null,
            new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Local)));
    }

    [Fact]
    public void MarkPaid_FromPending_MovesToPaid()
    {
        var payment = PendingPayment();

        payment.MarkPaid("pi_123", "succeeded", Now.AddMinutes(1));

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal("pi_123", payment.ProviderPaymentId);
        Assert.Equal("succeeded", payment.ProviderStatus);
        Assert.Equal(Now.AddMinutes(1), payment.PaidAtUtc);
    }

    [Fact]
    public void MarkPaid_WhenAlreadyPaid_Throws()
    {
        var payment = PendingPayment();
        payment.MarkPaid("pi_123", "succeeded", Now.AddMinutes(1));

        Assert.Throws<ConflictException>(() => payment.MarkPaid("pi_123", "succeeded", Now.AddMinutes(2)));
    }

    [Fact]
    public void MarkFailed_FromPending_RecordsTheReason()
    {
        var payment = PendingPayment();

        payment.MarkFailed("pi_123", "requires_payment_method", "Card declined", Now.AddMinutes(1));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("Card declined", payment.FailureReason);
        Assert.True(payment.IsTerminal);
    }

    [Fact]
    public void MarkFailed_WithoutReason_UsesTheDefaultMessage()
    {
        var payment = PendingPayment();

        payment.MarkFailed(null, null, "   ", Now.AddMinutes(1));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("The provider rejected the charge.", payment.FailureReason);
    }

    [Fact]
    public void RegisterRefund_OnPaidPayment_UpdatesTheRefundedAmount()
    {
        var payment = PendingPayment();
        payment.MarkPaid("pi_123", "succeeded", Now.AddMinutes(1));

        payment.RegisterRefund(new Money(40m, "BRL"), Now.AddMinutes(2));

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(40m, payment.RefundedAmount.Amount);
        Assert.Equal(60m, payment.RefundableAmount.Amount);
    }

    [Fact]
    public void RegisterRefund_OfTheWholeAmount_MovesToRefunded()
    {
        var payment = PendingPayment();
        payment.MarkPaid("pi_123", "succeeded", Now.AddMinutes(1));

        payment.RegisterRefund(new Money(100m, "BRL"), Now.AddMinutes(2));

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(0m, payment.RefundableAmount.Amount);
    }

    [Fact]
    public void RegisterRefund_AboveTheRefundableAmount_Throws()
    {
        var payment = PendingPayment();
        payment.MarkPaid("pi_123", "succeeded", Now.AddMinutes(1));
        payment.RegisterRefund(new Money(60m, "BRL"), Now.AddMinutes(2));

        Assert.Throws<ValidationException>(() => payment.RegisterRefund(new Money(50m, "BRL"), Now.AddMinutes(3)));
    }

    [Fact]
    public void RegisterRefund_WithAnotherCurrency_Throws()
    {
        var payment = PendingPayment();
        payment.MarkPaid("pi_123", "succeeded", Now.AddMinutes(1));

        Assert.Throws<ValidationException>(() => payment.RegisterRefund(new Money(10m, "USD"), Now.AddMinutes(2)));
    }

    [Fact]
    public void RegisterRefund_OnPendingPayment_Throws()
    {
        var payment = PendingPayment();

        Assert.Throws<ConflictException>(() => payment.RegisterRefund(new Money(10m, "BRL"), Now.AddMinutes(1)));
    }

    [Fact]
    public void Cancel_FromPending_MovesToCancelled()
    {
        var payment = PendingPayment();

        payment.Cancel("Customer changed their mind", Now.AddMinutes(1));

        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.Equal("Customer changed their mind", payment.FailureReason);
        Assert.Equal(Now.AddMinutes(1), payment.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_WhenPaid_Throws()
    {
        var payment = PendingPayment();
        payment.MarkPaid("pi_123", "succeeded", Now.AddMinutes(1));

        Assert.Throws<ConflictException>(() => payment.Cancel("Too late", Now.AddMinutes(2)));
    }

    [Fact]
    public void Cancel_WithoutReason_Throws()
    {
        var payment = PendingPayment();

        Assert.Throws<ValidationException>(() => payment.Cancel("  ", Now.AddMinutes(1)));
    }

    [Fact]
    public void SyncStatus_FromPendingToPaid_AppliesTheTransition()
    {
        var payment = PendingPayment();

        payment.SyncStatus(PaymentStatus.Paid, "pi_123", "succeeded", Now.AddMinutes(1));

        Assert.Equal(PaymentStatus.Paid, payment.Status);
    }

    [Fact]
    public void SyncStatus_ToATerminalStatus_IsIgnored()
    {
        var payment = PendingPayment();
        payment.MarkFailed("pi_123", "rejected", "Card declined", Now.AddMinutes(1));

        payment.SyncStatus(PaymentStatus.Paid, "pi_123", "succeeded", Now.AddMinutes(2));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
    }

    [Fact]
    public void AttachProviderPayload_KeepsTheRawAnswer()
    {
        var payment = PendingPayment();

        payment.AttachProviderPayload("{\"id\":\"pi_123\"}");

        Assert.Equal("{\"id\":\"pi_123\"}", payment.ProviderPayload);
    }

    [Fact]
    public void AttachProviderPayload_TruncatesLongPayloads()
    {
        var payment = PendingPayment();

        payment.AttachProviderPayload(new string('x', Payment.MaxProviderPayloadLength + 50));

        Assert.Equal(Payment.MaxProviderPayloadLength, payment.ProviderPayload!.Length);
    }
}
