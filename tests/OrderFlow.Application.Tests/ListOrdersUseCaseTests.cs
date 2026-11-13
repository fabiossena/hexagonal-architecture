using OrderFlow.Application.Orders.ListOrders;
using OrderFlow.Application.Tests.Fakes;
using OrderFlow.Domain.Orders;
using Xunit;

namespace OrderFlow.Application.Tests;

/// <summary>Behaviour of <see cref="ListOrdersUseCase"/> with a mocked repository.</summary>
public sealed class ListOrdersUseCaseTests
{
    [Fact]
    public async Task Execute_FiltersByStatus()
    {
        var repository = new InMemoryOrderRepository();
        await repository.AddAsync(OrderFactory.Draft(), CancellationToken.None);
        await repository.AddAsync(OrderFactory.Confirmed(), CancellationToken.None);
        await repository.AddAsync(OrderFactory.Cancelled(), CancellationToken.None);

        var useCase = new ListOrdersUseCase(repository);

        var result = await useCase.ExecuteAsync(new ListOrdersQuery(Status: OrderStatus.Confirmed));

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Confirmed", result.Items[0].Status);
    }

    [Fact]
    public async Task Execute_ClampsPageSizeToMaximum()
    {
        var useCase = new ListOrdersUseCase(new InMemoryOrderRepository());

        var result = await useCase.ExecuteAsync(new ListOrdersQuery(Page: 1, Size: 500));

        Assert.Equal(ListOrdersUseCase.MaxPageSize, result.Size);
    }

    [Fact]
    public async Task Execute_ClampsPageToAtLeastOne()
    {
        var useCase = new ListOrdersUseCase(new InMemoryOrderRepository());

        var result = await useCase.ExecuteAsync(new ListOrdersQuery(Page: 0, Size: 10));

        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task Execute_ComputesPagingMetadata()
    {
        var repository = new InMemoryOrderRepository();
        for (var index = 0; index < 5; index++)
        {
            await repository.AddAsync(OrderFactory.Draft(), CancellationToken.None);
        }

        var useCase = new ListOrdersUseCase(repository);

        var result = await useCase.ExecuteAsync(new ListOrdersQuery(Page: 2, Size: 2));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }
}
