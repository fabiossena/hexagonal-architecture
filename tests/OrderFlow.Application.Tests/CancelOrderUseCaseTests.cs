using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Orders.CancelOrder;
using OrderFlow.Application.Tests.Fakes;
using OrderFlow.Domain.Exceptions;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of <see cref="CancelOrderUseCase"/> with mocked ports.</summary>
public sealed class CancelOrderUseCaseTests
{
    [Fact]
    public async Task Execute_WhenDraft_CancelsAndRecordsReason()
    {
        var repository = new InMemoryOrderRepository();
        var order = OrderFactory.Draft();
        await repository.AddAsync(order, CancellationToken.None);

        var unitOfWork = new FakeUnitOfWork();
        var useCase = new CancelOrderUseCase(
            repository,
            unitOfWork,
            new FixedClock(OrderFactory.Now.AddMinutes(3)));

        var result = await useCase.ExecuteAsync(order.Id, "Customer requested cancellation");

        Assert.Equal("Cancelled", result.Status);
        Assert.Equal("Customer requested cancellation", result.CancellationReason);
        Assert.NotNull(result.CancelledAtUtc);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Execute_WhenOrderDoesNotExist_ThrowsNotFound()
    {
        var useCase = new CancelOrderUseCase(
            new InMemoryOrderRepository(),
            new FakeUnitOfWork(),
            new FixedClock(OrderFactory.Now));

        await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid(), "reason"));
    }

    [Fact]
    public async Task Execute_WhenAlreadyCancelled_ThrowsConflict()
    {
        var repository = new InMemoryOrderRepository();
        var order = OrderFactory.Cancelled();
        await repository.AddAsync(order, CancellationToken.None);

        var useCase = new CancelOrderUseCase(
            repository,
            new FakeUnitOfWork(),
            new FixedClock(OrderFactory.Now.AddMinutes(5)));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(order.Id, "again"));
    }

    [Fact]
    public async Task Execute_WithoutReason_ThrowsValidation()
    {
        var repository = new InMemoryOrderRepository();
        var order = OrderFactory.Draft();
        await repository.AddAsync(order, CancellationToken.None);

        var useCase = new CancelOrderUseCase(
            repository,
            new FakeUnitOfWork(),
            new FixedClock(OrderFactory.Now));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(order.Id, "   "));
    }
}
