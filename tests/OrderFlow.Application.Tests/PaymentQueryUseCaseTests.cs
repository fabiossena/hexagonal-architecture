using OrderFlow.Application.Payments.GetPayment;
using OrderFlow.Application.Payments.ListPayments;
using OrderFlow.Application.Tests.Fakes;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.ValueObjects;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of the payment read use cases with mocked ports.</summary>
public sealed class PaymentQueryUseCaseTests
{
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly Guid _orderId = Guid.NewGuid();

    private Payment SeedPayment(PaymentStatus status, PaymentProvider provider = PaymentProvider.Simulated)
    {
        var payment = Payment.Create(
            _orderId,
            provider,
            PaymentMethod.Pix,
            new Money(10m, "BRL"),
            "Order 1234",
            null,
            OrderFactory.Now);

        if (status == PaymentStatus.Paid)
        {
            payment.MarkPaid("sim_1", "approved", OrderFactory.Now.AddMinutes(1));
        }
        else if (status == PaymentStatus.Failed)
        {
            payment.MarkFailed("sim_2", "rejected", "Card declined", OrderFactory.Now.AddMinutes(1));
        }

        _payments.Seed(payment);
        return payment;
    }

    [Fact]
    public async Task Get_WithAKnownPayment_ReturnsTheReadModel()
    {
        var payment = SeedPayment(PaymentStatus.Paid);

        var result = await new GetPaymentUseCase(_payments).ExecuteAsync(payment.Id);

        Assert.Equal(payment.Id, result.Id);
        Assert.Equal("Paid", result.Status);
        Assert.Equal("sim_1", result.ProviderPaymentId);
    }

    [Fact]
    public async Task Get_WithAnUnknownPayment_Throws()
    {
        await Assert.ThrowsAsync<PaymentNotFoundException>(() =>
            new GetPaymentUseCase(_payments).ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_ReturnsEveryPayment()
    {
        SeedPayment(PaymentStatus.Paid);
        SeedPayment(PaymentStatus.Failed);

        var result = await new ListPaymentsUseCase(_payments).ExecuteAsync(new ListPaymentsQuery());

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        SeedPayment(PaymentStatus.Paid);
        SeedPayment(PaymentStatus.Failed);

        var result = await new ListPaymentsUseCase(_payments)
            .ExecuteAsync(new ListPaymentsQuery(Status: PaymentStatus.Paid));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Paid", result.Items[0].Status);
    }

    [Fact]
    public async Task List_FiltersByProvider()
    {
        SeedPayment(PaymentStatus.Paid);
        SeedPayment(PaymentStatus.Paid, PaymentProvider.Stripe);

        var result = await new ListPaymentsUseCase(_payments)
            .ExecuteAsync(new ListPaymentsQuery(Provider: PaymentProvider.Stripe));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Stripe", result.Items[0].Provider);
    }

    [Fact]
    public async Task List_ClampsThePaging()
    {
        SeedPayment(PaymentStatus.Paid);

        var result = await new ListPaymentsUseCase(_payments)
            .ExecuteAsync(new ListPaymentsQuery(Page: 0, Size: 5000));

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.Size);
    }
}
