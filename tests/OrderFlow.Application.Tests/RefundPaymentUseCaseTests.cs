using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Payments.RefundPayment;
using OrderFlow.Application.Tests.Fakes;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.ValueObjects;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of <see cref="RefundPaymentUseCase"/> with mocked ports.</summary>
public sealed class RefundPaymentUseCaseTests
{
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePaymentProvider _provider = new(PaymentProvider.Simulated);

    private RefundPaymentUseCase CreateUseCase()
    {
        return new RefundPaymentUseCase(
            _payments,
            new FakePaymentProviderResolver(_provider),
            _unitOfWork,
            new FixedClock(OrderFactory.Now));
    }

    private Payment SeedPayment(PaymentStatus status = PaymentStatus.Paid)
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            PaymentProvider.Simulated,
            PaymentMethod.Pix,
            new Money(100m, "BRL"),
            "Order 1234",
            "buyer@example.com",
            OrderFactory.Now);

        if (status == PaymentStatus.Paid)
        {
            payment.MarkPaid("sim_1", "approved", OrderFactory.Now.AddMinutes(1));
        }

        _payments.Seed(payment);
        return payment;
    }

    [Fact]
    public async Task Execute_WithoutAmount_RefundsEverything()
    {
        var payment = SeedPayment();

        var result = await CreateUseCase().ExecuteAsync(payment.Id);

        Assert.Equal("Refunded", result.Status);
        Assert.Equal(100m, result.RefundedAmount.Amount);
        Assert.Equal(0m, result.RefundableAmount.Amount);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Execute_WithAPartialAmount_RefundsPartially()
    {
        var payment = SeedPayment();

        var result = await CreateUseCase().ExecuteAsync(payment.Id, 30m);

        Assert.Equal("Paid", result.Status);
        Assert.Equal(30m, result.RefundedAmount.Amount);
        Assert.Equal(70m, result.RefundableAmount.Amount);
    }

    [Fact]
    public async Task Execute_OnAPendingPayment_Throws()
    {
        var payment = SeedPayment(PaymentStatus.Processing);

        await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase().ExecuteAsync(payment.Id));
    }

    [Fact]
    public async Task Execute_WithAnUnknownPayment_Throws()
    {
        await Assert.ThrowsAsync<PaymentNotFoundException>(() => CreateUseCase().ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Execute_AboveTheRefundableAmount_Throws()
    {
        var payment = SeedPayment();

        await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase().ExecuteAsync(payment.Id, 500m));
    }

    [Fact]
    public async Task Execute_WhenTheProviderRefuses_Throws()
    {
        var payment = SeedPayment();
        _provider.RefundResult = new ProviderRefundResult(false, null, "rejected", "The provider refused the refund.");

        await Assert.ThrowsAsync<PaymentProviderException>(() => CreateUseCase().ExecuteAsync(payment.Id));
    }
}
