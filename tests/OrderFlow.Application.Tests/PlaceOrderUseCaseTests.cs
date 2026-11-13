using OrderFlow.Application.Orders.PlaceOrder;
using OrderFlow.Application.Tests.Fakes;
using OrderFlow.Domain.Exceptions;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of <see cref="PlaceOrderUseCase"/> with mocked ports.</summary>
public sealed class PlaceOrderUseCaseTests
{
    private static readonly DateTime Now = new(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Execute_WithItems_PersistsAndReturnsDto()
    {
        var repository = new InMemoryOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new PlaceOrderUseCase(repository, unitOfWork, new FixedClock(Now));

        var result = await useCase.ExecuteAsync(new PlaceOrderCommand(
            CustomerId,
            new[]
            {
                new PlaceOrderItem("Mechanical keyboard", 2, 50m),
                new PlaceOrderItem("Mouse", 1, 20m)
            }));

        var stored = Assert.Single(repository.Orders);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal(CustomerId, result.CustomerId);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(120m, result.Subtotal.Amount);
        Assert.Equal(120m, result.Total.Amount);
        Assert.Equal(Now, result.CreatedAtUtc);
    }

    [Fact]
    public async Task Execute_ComputesVolumeDiscount()
    {
        var useCase = new PlaceOrderUseCase(
            new InMemoryOrderRepository(),
            new FakeUnitOfWork(),
            new FixedClock(Now));

        var result = await useCase.ExecuteAsync(new PlaceOrderCommand(
            CustomerId,
            new[]
            {
                new PlaceOrderItem("A", 1, 400m),
                new PlaceOrderItem("B", 1, 400m),
                new PlaceOrderItem("C", 1, 400m)
            }));

        Assert.Equal(1200m, result.Subtotal.Amount);
        Assert.Equal(60m, result.Discount.Amount);
        Assert.Equal(1140m, result.Total.Amount);
    }

    [Fact]
    public async Task Execute_WithoutItems_Throws()
    {
        var useCase = new PlaceOrderUseCase(
            new InMemoryOrderRepository(),
            new FakeUnitOfWork(),
            new FixedClock(Now));

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new PlaceOrderCommand(CustomerId, Array.Empty<PlaceOrderItem>())));
    }

    [Fact]
    public async Task Execute_WithNullItems_Throws()
    {
        var useCase = new PlaceOrderUseCase(
            new InMemoryOrderRepository(),
            new FakeUnitOfWork(),
            new FixedClock(Now));

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new PlaceOrderCommand(CustomerId, null!)));
    }

    [Fact]
    public async Task Execute_WithNullCommand_Throws()
    {
        var useCase = new PlaceOrderUseCase(
            new InMemoryOrderRepository(),
            new FakeUnitOfWork(),
            new FixedClock(Now));

        await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(null!));
    }

    [Fact]
    public async Task Execute_WithInvalidCurrency_Throws()
    {
        var useCase = new PlaceOrderUseCase(
            new InMemoryOrderRepository(),
            new FakeUnitOfWork(),
            new FixedClock(Now));

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new PlaceOrderCommand(
                CustomerId,
                new[] { new PlaceOrderItem("A", 1, 10m) },
                Currency: "EU")));
    }
}
