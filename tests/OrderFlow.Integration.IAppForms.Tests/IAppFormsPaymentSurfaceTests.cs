using Microsoft.Extensions.Options;
using OrderFlow.Application.Payments.ApplyProviderCallback;
using OrderFlow.Application.Payments.CreatePayment;
using OrderFlow.Application.Payments.GetPayment;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.ValueObjects;
using OrderFlow.Integration.IAppForms.Tests.Fakes;
using Xunit;

namespace OrderFlow.Integration.IAppForms.Tests;

/// <summary>Behaviour of the iAppForms checkout surface.</summary>
public sealed class IAppFormsPaymentSurfaceTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid QuoteId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    private readonly InMemoryOrderRepository _orders = new();
    private readonly InMemoryPaymentRepository _payments = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePaymentProvider _provider = new(PaymentProvider.Simulated);

    private IAppFormsPaymentSurface CreateSurface()
    {
        var resolver = new FakePaymentProviderResolver(_provider);
        var clock = new FixedClock(Now);

        return new IAppFormsPaymentSurface(
            _orders,
            new CreatePaymentUseCase(_payments, _orders, resolver, _unitOfWork, clock),
            new GetPaymentUseCase(_payments),
            new ApplyProviderCallbackUseCase(_payments, resolver, _unitOfWork, clock),
            resolver,
            Options.Create(new IAppFormsOptions()));
    }

    private Order SeedConfirmedQuote()
    {
        var order = Order.Create(Guid.NewGuid(), Now, QuoteId.ToString());
        order.AddItem("iAppForms plan", 1, new Money(120m, "BRL"));
        order.Confirm(Now.AddMinutes(1));
        _orders.Seed(order);
        return order;
    }

    [Fact]
    public async Task Create_WithAnUnknownQuote_ThrowsNotFound()
    {
        var exception = await Assert.ThrowsAsync<CheckoutException>(() =>
            CreateSurface().CreateAsync(new PaymentSurfaceRequest(QuoteId, "Pix"), "key-1"));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(CheckoutErrorCodes.QuoteNotFound, exception.Code);
    }

    [Fact]
    public async Task Create_WithAnUnsupportedMethod_ThrowsUnsupportedCombination()
    {
        SeedConfirmedQuote();

        var exception = await Assert.ThrowsAsync<CheckoutException>(() =>
            CreateSurface().CreateAsync(new PaymentSurfaceRequest(QuoteId, "Crypto"), "key-1"));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(CheckoutErrorCodes.UnsupportedCombination, exception.Code);
    }

    [Fact]
    public async Task Create_ChargesTheQuoteAndAnswersInTheIAppFormsVocabulary()
    {
        var order = SeedConfirmedQuote();

        var result = await CreateSurface().CreateAsync(
            new PaymentSurfaceRequest(QuoteId, "Pix", DomainId: Guid.NewGuid()),
            "key-1");

        Assert.Equal("InReview", result.Status);
        Assert.Equal("Pending", result.ProviderStatus);
        Assert.Equal("Simulated", result.Provider);
        Assert.False(result.Reused);

        var stored = Assert.Single(_payments.Payments);
        Assert.Equal(order.Id, stored.OrderId);
        Assert.Equal(120m, stored.Amount.Amount);
        Assert.Equal("key-1", stored.IdempotencyKey);
    }

    [Fact]
    public async Task Create_ReplayingTheIdempotencyKey_ReusesThePayment()
    {
        SeedConfirmedQuote();
        var surface = CreateSurface();

        var first = await surface.CreateAsync(new PaymentSurfaceRequest(QuoteId, "Pix"), "key-1");
        var second = await surface.CreateAsync(new PaymentSurfaceRequest(QuoteId, "Pix"), "key-1");

        Assert.True(second.Reused);
        Assert.Equal(first.PaymentId, second.PaymentId);
        Assert.Single(_payments.Payments);
    }

    [Fact]
    public async Task Create_ReplayingTheKeyOnAnotherQuote_Throws()
    {
        SeedConfirmedQuote();

        var other = Order.Create(Guid.NewGuid(), Now, Guid.NewGuid().ToString());
        other.AddItem("Another plan", 1, new Money(10m, "BRL"));
        other.Confirm(Now.AddMinutes(1));
        _orders.Seed(other);

        var surface = CreateSurface();
        await surface.CreateAsync(new PaymentSurfaceRequest(QuoteId, "Pix"), "key-1");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            surface.CreateAsync(
                new PaymentSurfaceRequest(Guid.Parse(other.ExternalReference!), "Pix"),
                "key-1"));

        Assert.Contains("idempotency key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatus_MapsTheOrderFlowStatus()
    {
        SeedConfirmedQuote();
        var surface = CreateSurface();

        var created = await surface.CreateAsync(new PaymentSurfaceRequest(QuoteId, "Pix"), "key-1");
        var status = await surface.GetStatusAsync(created.PaymentId);

        Assert.Equal(created.PaymentId, status.PaymentId);
        Assert.Equal("InReview", status.Status);
        Assert.Equal("Pending", status.ProviderStatus);
        Assert.Equal("Pix", status.MethodCode);
        Assert.Equal(120m, status.Amount);
        Assert.Equal("BRL", status.Currency);
        Assert.Equal(new[] { "cancel" }, status.SupportedOperations);
        Assert.Equal(Now, status.RequestedAtUtc);
    }

    [Fact]
    public async Task ApplyWebhook_ForAnUnknownPayment_ReturnsFalse()
    {
        var applied = await CreateSurface().ApplyWebhookAsync("Simulated", "sim_unknown", "approved");

        Assert.False(applied);
    }

    [Fact]
    public async Task ApplyWebhook_UpdatesThePayment()
    {
        SeedConfirmedQuote();
        var surface = CreateSurface();

        var created = await surface.CreateAsync(new PaymentSurfaceRequest(QuoteId, "Pix"), "key-1");

        var applied = await surface.ApplyWebhookAsync(
            "Simulated",
            _payments.Payments[0].ProviderPaymentId!,
            "approved");

        Assert.True(applied);

        var status = await surface.GetStatusAsync(created.PaymentId);
        Assert.Equal("Approved", status.Status);
        Assert.Equal("Paid", status.ProviderStatus);
        Assert.Equal(new[] { "refund" }, status.SupportedOperations);
    }

    [Fact]
    public void EnsureAuthorized_WithAnApiKeyConfigured_RejectsAMissingKey()
    {
        var surface = CreateSurface();

        // No key configured: the surface is open (development).
        surface.EnsureAuthorized(null);

        var locked = new IAppFormsPaymentSurface(
            _orders,
            new CreatePaymentUseCase(
                _payments,
                _orders,
                new FakePaymentProviderResolver(_provider),
                _unitOfWork,
                new FixedClock(Now)),
            new GetPaymentUseCase(_payments),
            new ApplyProviderCallbackUseCase(
                _payments,
                new FakePaymentProviderResolver(_provider),
                _unitOfWork,
                new FixedClock(Now)),
            new FakePaymentProviderResolver(_provider),
            Options.Create(new IAppFormsOptions { ApiKey = "s3cret" }));

        var exception = Assert.Throws<CheckoutException>(() => locked.EnsureAuthorized(null));
        Assert.Equal(401, exception.StatusCode);
    }
}
