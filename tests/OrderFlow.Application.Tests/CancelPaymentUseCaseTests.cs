using OrderFlow.Application.Payments.CancelPayment;
using OrderFlow.Application.Tests.Fakes;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.ValueObjects;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of <see cref="CancelPaymentUseCase"/> with mocked ports.</summary>
public sealed class CancelPaymentUseCaseTests
{
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Payment SeedPendingPayment()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            PaymentProvider.Simulated,
            PaymentMethod.Pix,
            new Money(100m, "BRL"),
            "Order 1234",
            null,
            OrderFactory.Now);

        _payments.Seed(payment);
        return payment;
    }

    [Fact]
    public async Task Execute_CancelsThePayment()
    {
        var payment = SeedPendingPayment();

        var result = await new CancelPaymentUseCase(
            _payments,
            _unitOfWork,
            new FixedClock(OrderFactory.Now)).ExecuteAsync(payment.Id, "Customer changed their mind");

        Assert.Equal("Cancelled", result.Status);
        Assert.Equal("Customer changed their mind", result.FailureReason);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Execute_WithoutReason_Throws()
    {
        var payment = SeedPendingPayment();

        await Assert.ThrowsAsync<ValidationException>(() => new CancelPaymentUseCase(
            _payments,
            _unitOfWork,
            new FixedClock(OrderFactory.Now)).ExecuteAsync(payment.Id, "  "));
    }
}
