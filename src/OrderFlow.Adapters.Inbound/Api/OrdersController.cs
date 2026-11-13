using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Adapters.Inbound.Api.Contracts;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Orders.CancelOrder;
using OrderFlow.Application.Orders.ConfirmOrder;
using OrderFlow.Application.Orders.GetOrder;
using OrderFlow.Application.Orders.ListOrders;
using OrderFlow.Application.Orders.PlaceOrder;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Adapters.Inbound.Api;

/// <summary>
/// Inbound HTTP adapter for the order use cases.
/// </summary>
/// <remarks>
/// The controller only translates HTTP ↔ application commands/queries: no
/// business rule lives here. Each action delegates to exactly one use case.
/// </remarks>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly PlaceOrderUseCase _placeOrder;
    private readonly ConfirmOrderUseCase _confirmOrder;
    private readonly CancelOrderUseCase _cancelOrder;
    private readonly GetOrderUseCase _getOrder;
    private readonly ListOrdersUseCase _listOrders;

    /// <summary>Creates the controller.</summary>
    /// <param name="placeOrder">Place-order use case.</param>
    /// <param name="confirmOrder">Confirm-order use case.</param>
    /// <param name="cancelOrder">Cancel-order use case.</param>
    /// <param name="getOrder">Get-order use case.</param>
    /// <param name="listOrders">List-orders use case.</param>
    public OrdersController(
        PlaceOrderUseCase placeOrder,
        ConfirmOrderUseCase confirmOrder,
        CancelOrderUseCase cancelOrder,
        GetOrderUseCase getOrder,
        ListOrdersUseCase listOrders)
    {
        _placeOrder = placeOrder;
        _confirmOrder = confirmOrder;
        _cancelOrder = cancelOrder;
        _getOrder = getOrder;
        _listOrders = listOrders;
    }

    /// <summary>Creates a draft order.</summary>
    /// <param name="request">Order data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created order.</returns>
    /// <response code="201">Order created.</response>
    /// <response code="400">The request violates a business rule.</response>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var items = request.Items?
            .Select(item => new PlaceOrderItem(
                item.ProductName ?? string.Empty,
                item.Quantity,
                item.UnitPrice))
            .ToList() ?? new List<PlaceOrderItem>();

        var command = new PlaceOrderCommand(
            request.CustomerId,
            items,
            request.Currency ?? Money.DefaultCurrency);

        var order = await _placeOrder.ExecuteAsync(command, cancellationToken);

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    /// <summary>Returns a single order.</summary>
    /// <param name="id">Order identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order.</returns>
    /// <response code="200">Order found.</response>
    /// <response code="404">Order not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _getOrder.ExecuteAsync(id, cancellationToken);
        return Ok(order);
    }

    /// <summary>Lists orders with pagination and an optional status filter.</summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="size">Page size (1..100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of orders.</returns>
    /// <response code="200">Page returned.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> ListOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _listOrders.ExecuteAsync(
            new ListOrdersQuery(page, size, status),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Confirms a draft order.</summary>
    /// <param name="id">Order identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The confirmed order.</returns>
    /// <response code="200">Order confirmed.</response>
    /// <response code="404">Order not found.</response>
    /// <response code="409">Order is not a draft.</response>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> ConfirmOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _confirmOrder.ExecuteAsync(id, cancellationToken);
        return Ok(order);
    }

    /// <summary>Cancels a draft or confirmed order.</summary>
    /// <param name="id">Order identity.</param>
    /// <param name="request">Cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancelled order.</returns>
    /// <response code="200">Order cancelled.</response>
    /// <response code="400">The reason is missing.</response>
    /// <response code="404">Order not found.</response>
    /// <response code="409">Order is already cancelled.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> CancelOrder(
        Guid id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await _cancelOrder.ExecuteAsync(
            id,
            request.Reason ?? string.Empty,
            cancellationToken);

        return Ok(order);
    }
}
