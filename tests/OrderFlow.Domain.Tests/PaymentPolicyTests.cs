using OrderFlow.Domain.Payments;
using OrderFlow.Domain.Policies;
using Xunit;

namespace OrderFlow.Domain.Tests;

/// <summary>Rules of <see cref="PaymentPolicy"/>: provider matrix and status machine.</summary>
public sealed class PaymentPolicyTests
{
    [Theory]
    [InlineData(PaymentProvider.Stripe, PaymentMethod.CreditCard, true)]
    [InlineData(PaymentProvider.Stripe, PaymentMethod.DebitCard, true)]
    [InlineData(PaymentProvider.Stripe, PaymentMethod.Pix, true)]
    [InlineData(PaymentProvider.Stripe, PaymentMethod.Boleto, true)]
    [InlineData(PaymentProvider.Stripe, PaymentMethod.BankTransfer, false)]
    [InlineData(PaymentProvider.MercadoPago, PaymentMethod.BankTransfer, true)]
    [InlineData(PaymentProvider.MercadoPago, PaymentMethod.Boleto, true)]
    [InlineData(PaymentProvider.Asaas, PaymentMethod.Pix, true)]
    [InlineData(PaymentProvider.Asaas, PaymentMethod.BankTransfer, false)]
    [InlineData(PaymentProvider.Simulated, PaymentMethod.BankTransfer, true)]
    public void Supports_ReflectsTheProviderMatrix(
        PaymentProvider provider,
        PaymentMethod method,
        bool expected)
    {
        Assert.Equal(expected, PaymentPolicy.Supports(provider, method));
    }

    [Fact]
    public void MethodsFor_ReturnsEveryMethodOfTheProvider()
    {
        var methods = PaymentPolicy.MethodsFor(PaymentProvider.Asaas);

        Assert.Equal(4, methods.Count);
        Assert.Contains(PaymentMethod.CreditCard, methods);
        Assert.Contains(PaymentMethod.Pix, methods);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Processing, true)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid, true)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Failed, true)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Cancelled, true)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Refunded, false)]
    [InlineData(PaymentStatus.Processing, PaymentStatus.Paid, true)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Refunded, true)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Pending, false)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Paid, false)]
    [InlineData(PaymentStatus.Cancelled, PaymentStatus.Paid, false)]
    [InlineData(PaymentStatus.Refunded, PaymentStatus.Paid, false)]
    public void CanTransition_FollowsTheStatusMachine(
        PaymentStatus from,
        PaymentStatus to,
        bool expected)
    {
        Assert.Equal(expected, PaymentPolicy.CanTransition(from, to));
    }

    [Theory]
    [InlineData(PaymentStatus.Failed, true)]
    [InlineData(PaymentStatus.Cancelled, true)]
    [InlineData(PaymentStatus.Refunded, true)]
    [InlineData(PaymentStatus.Pending, false)]
    [InlineData(PaymentStatus.Processing, false)]
    [InlineData(PaymentStatus.Paid, false)]
    public void IsTerminal_IdentifiesTheFinalStatuses(PaymentStatus status, bool expected)
    {
        Assert.Equal(expected, PaymentPolicy.IsTerminal(status));
    }
}
