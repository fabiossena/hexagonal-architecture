using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Payments.CreatePayment;
using OrderFlow.Application.Tests.Fakes;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Payments;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of <see cref="CreatePaymentUseCase"/> with mocked ports.</summary>
public sealed class CreatePaymentUseCaseTests
{
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly InMemoryOrderRepository _orders = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePaymentProvider _provider = new(PaymentProvider.Simulated);

    private CreatePaymentUseCase CreateUseCase()
    {
        return new CreatePaymentUseCase(
            _payments,
            _orders,
            new FakePaymentProviderResolver(_provider),
            _unitOfWork,
            new FixedClock(OrderFactory.Now));
    }

    private Order SeedConfirmedOrder()
    {
        var order = OrderFactory.Confirmed();
        _orders.Seed(order);
        return order;
    }

    [Fact]
    public async Task Execute_WithConfirmedOrder_PersistsThePayment()
    {
        var order = SeedConfirmedOrder();
        _provider.SupportedMethods = new[] { PaymentMethod.Pix };

        var result = await CreateUseCase().ExecuteAsync(new CreatePaymentCommand(
            order.Id,
            PaymentProvider.Simulated,
            PaymentMethod.Pix,
            Amount: 50m,
            Description: "Partial payment",
            CustomerEmail: "buyer@example.com"));

        var stored = Assert.Single(_payments.Payments);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal("Paid", result.Status);
        Assert.Equal(50m, result.Amount.Amount);
        Assert.Equal("USD", result.Amount.Currency);
        Assert.Equal("Partial payment", result.Description);
        Assert.Equal("buyer@example.com", result.CustomerEmail);
        Assert.Equal("sim_1", result.ProviderPaymentId);
    }

    [Fact]
    public async Task Execute_WithoutAmount_UsesTheOrderTotal()
    {
        var order = SeedConfirmedOrder();

        var result = await CreateUseCase().ExecuteAsync(new CreatePaymentCommand(
            order.Id,
            PaymentProvider.Simulated,
            PaymentMethod.Pix));

        Assert.Equal(order.Total.Amount, result.Amount.Amount);
    }

    [Fact]
    public async Task Execute_ForwardsTheChargeToTheProvider()
    {
        var order = SeedConfirmedOrder();
        _provider.SupportedMethods = new[] { PaymentMethod.CreditCard };

        await CreateUseCase().ExecuteAsync(new CreatePaymentCommand(
            order.Id,
            PaymentProvider.Simulated,
            PaymentMethod.CreditCard,
            Amount: 10m,
            CustomerName: "Test Buyer",
            CustomerDocument: "12345678901",
            CardToken: "tok_123",
            Installments: 3,
            CallbackUrl: "http://localhost/api/webhooks/simulated",
            IdempotencyKey: "key-123"));

        var request = _provider.LastRequest!;
        Assert.Equal(order.Id, request.OrderId);
        Assert.Equal(10m, request.Amount.Amount);
        Assert.Equal(PaymentMethod.CreditCard, request.Method);
        Assert.Equal(3, request.Installments);
        Assert.Equal("tok_123", request.CardToken);
        Assert.Equal("12345678901", request.CustomerDocument);
        Assert.Equal("key-123", request.IdempotencyKey);
        Assert.Equal("http://localhost/api/webhooks/simulated", request.CallbackUrl);
        Assert.Equal(order.Id.ToString("N"), request.Metadata["order_id"]);
    }

    [Fact]
    public async Task Execute_WithDraftOrder_Throws()
    {
        var order = OrderFactory.Draft();
        _orders.Seed(order);

        await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase().ExecuteAsync(
            new CreatePaymentCommand(order.Id, PaymentProvider.Simulated, PaymentMethod.Pix)));
    }

    [Fact]
    public async Task Execute_WithUnknownOrder_Throws()
    {
        await Assert.ThrowsAsync<OrderNotFoundException>(() => CreateUseCase().ExecuteAsync(
            new CreatePaymentCommand(Guid.NewGuid(), PaymentProvider.Simulated, PaymentMethod.Pix)));
    }

    [Fact]
    public async Task Execute_AboveTheOrderTotal_Throws()
    {
        var order = SeedConfirmedOrder();

        await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase().ExecuteAsync(
            new CreatePaymentCommand(order.Id, PaymentProvider.Simulated, PaymentMethod.Pix, Amount: 5000m)));
    }

    [Fact]
    public async Task Execute_WithUnsupportedMethod_Throws()
    {
        var order = SeedConfirmedOrder();
        _provider.SupportedMethods = new[] { PaymentMethod.CreditCard };

        await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase().ExecuteAsync(
            new CreatePaymentCommand(order.Id, PaymentProvider.Simulated, PaymentMethod.Pix)));
    }

    [Fact]
    public async Task Execute_WithUnconfiguredProvider_Throws()
    {
        var order = SeedConfirmedOrder();
        var provider = new FakePaymentProvider(PaymentProvider.Stripe, isConfigured: false);

        var useCase = new CreatePaymentUseCase(
            _payments,
            _orders,
            new FakePaymentProviderResolver(provider),
            _unitOfWork,
            new FixedClock(OrderFactory.Now));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(
            new CreatePaymentCommand(order.Id, PaymentProvider.Stripe, PaymentMethod.CreditCard)));
    }

    [Fact]
    public async Task Execute_WhenTheProviderFails_RecordsTheAttemptAndRethrows()
    {
        var order = SeedConfirmedOrder();
        _provider.ChargeException = new PaymentProviderException("Stripe could not be reached.", "{\"error\":\"timeout\"}");

        await Assert.ThrowsAsync<PaymentProviderException>(() => CreateUseCase().ExecuteAsync(
            new CreatePaymentCommand(order.Id, PaymentProvider.Simulated, PaymentMethod.Pix)));

        var stored = Assert.Single(_payments.Payments);
        Assert.Equal(PaymentStatus.Failed, stored.Status);
        Assert.Equal("Stripe could not be reached.", stored.FailureReason);
        Assert.Equal("{\"error\":\"timeout\"}", stored.ProviderPayload);
    }

    [Fact]
    public async Task Execute_WithARejectedCharge_StoresTheFailedStatus()
    {
        var order = SeedConfirmedOrder();
        _provider.ChargeResult = new ProviderChargeResult(
            "sim_2",
            PaymentStatus.Failed,
            "rejected",
            FailureReason: "Simulated failure requested by the test screen.");

        var result = await CreateUseCase().ExecuteAsync(new CreatePaymentCommand(
            order.Id,
            PaymentProvider.Simulated,
            PaymentMethod.Pix,
            SimulateFailure: true));

        Assert.Equal("Failed", result.Status);
        Assert.Equal("Simulated failure requested by the test screen.", result.FailureReason);
        Assert.Equal("sim_2", result.ProviderPaymentId);
    }

    [Fact]
    public async Task Execute_WithAPendingCharge_KeepsThePaymentPending()
    {
        var order = SeedConfirmedOrder();
        _provider.ChargeResult = new ProviderChargeResult(
            "sim_3",
            PaymentStatus.Processing,
            "pending",
            CheckoutUrl: "https://checkout.example/sim_3");

        var result = await CreateUseCase().ExecuteAsync(new CreatePaymentCommand(
            order.Id,
            PaymentProvider.Simulated,
            PaymentMethod.Pix));

        Assert.Equal("Processing", result.Status);
        Assert.Equal("https://checkout.example/sim_3", result.CheckoutUrl);
    }
}
