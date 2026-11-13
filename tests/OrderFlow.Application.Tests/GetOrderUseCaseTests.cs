using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Orders.GetOrder;
using OrderFlow.Application.Tests.Fakes;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of <see cref="GetOrderUseCase"/> with a mocked repository.</summary>
public sealed class GetOrderUseCaseTests
{
    [Fact]
    public async Task Execute_WhenOrderExists_ReturnsDto()
    {
        var repository = new InMemoryOrderRepository();
        var order = OrderFactory.Confirmed();
        await repository.AddAsync(order, CancellationToken.None);

        var useCase = new GetOrderUseCase(repository);

        var result = await useCase.ExecuteAsync(order.Id);

        Assert.Equal(order.Id, result.Id);
        Assert.Equal("Confirmed", result.Status);
        Assert.Single(result.Items);
        Assert.Equal(100m, result.Total.Amount);
    }

    [Fact]
    public async Task Execute_WhenOrderDoesNotExist_ThrowsNotFound()
    {
        var useCase = new GetOrderUseCase(new InMemoryOrderRepository());

        await Assert.ThrowsAsync<OrderNotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }
}
