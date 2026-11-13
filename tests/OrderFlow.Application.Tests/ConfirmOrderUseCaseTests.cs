using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Orders.ConfirmOrder;
using OrderFlow.Application.Tests.Fakes;
using OrderFlow.Domain.Exceptions;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of <see cref="ConfirmOrderUseCase"/> with mocked ports.</summary>
public sealed class ConfirmOrderUseCaseTests
{
    [Fact]
    public async Task Execute_WhenDraft_ConfirmsAndCommits()
    {
        var repository = new InMemoryOrderRepository();
        var order = OrderFactory.Draft();
        await repository.AddAsync(order, CancellationToken.None);

        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ConfirmOrderUseCase(
            repository,
            unitOfWork,
            new FixedClock(OrderFactory.Now.AddMinutes(5)));

        var result = await useCase.ExecuteAsync(order.Id);

        Assert.Equal("Confirmed", result.Status);
        Assert.NotNull(result.ConfirmedAtUtc);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Execute_WhenOrderDoesNotExist_ThrowsNotFound()
    {
        var useCase = new ConfirmOrderUseCase(
            new InMemoryOrderRepository(),
            new FakeUnitOfWork(),
            new FixedClock(OrderFactory.Now));

        await Assert.ThrowsAsync<OrderNotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Execute_WhenAlreadyConfirmed_ThrowsConflict()
    {
        var repository = new InMemoryOrderRepository();
        var order = OrderFactory.Confirmed();
        await repository.AddAsync(order, CancellationToken.None);

        var useCase = new ConfirmOrderUseCase(
            repository,
            new FakeUnitOfWork(),
            new FixedClock(OrderFactory.Now.AddMinutes(5)));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(order.Id));
    }
}
