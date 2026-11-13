using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OrderFlow.Adapters.Inbound.Api.Contracts;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Payments.CancelPayment;
using OrderFlow.Application.Payments.CreatePayment;
using OrderFlow.Application.Payments.GetPayment;
using OrderFlow.Application.Payments.ListPayments;
using OrderFlow.Application.Payments.RefreshPaymentStatus;
using OrderFlow.Application.Payments.RefundPayment;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Adapters.Inbound.Api;

/// <summary>
/// Inbound HTTP adapter for the payment use cases.
/// </summary>
/// <remarks>
/// The controller only translates HTTP ↔ application commands: no business rule
/// lives here. Each action delegates to exactly one use case.
/// </remarks>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController : ControllerBase
{
    private readonly CreatePaymentUseCase _createPayment;
    private readonly GetPaymentUseCase _getPayment;
    private readonly ListPaymentsUseCase _listPayments;
    private readonly RefundPaymentUseCase _refundPayment;
    private readonly CancelPaymentUseCase _cancelPayment;
    private readonly RefreshPaymentStatusUseCase _refreshPaymentStatus;
    private readonly IConfiguration _configuration;

    /// <summary>Creates the controller.</summary>
    /// <param name="createPayment">Create-payment use case.</param>
    /// <param name="getPayment">Get-payment use case.</param>
    /// <param name="listPayments">List-payments use case.</param>
    /// <param name="refundPayment">Refund-payment use case.</param>
    /// <param name="cancelPayment">Cancel-payment use case.</param>
    /// <param name="refreshPaymentStatus">Refresh-payment-status use case.</param>
    /// <param name="configuration">Application configuration (callback URL).</param>
    public PaymentsController(
        CreatePaymentUseCase createPayment,
        GetPaymentUseCase getPayment,
        ListPaymentsUseCase listPayments,
        RefundPaymentUseCase refundPayment,
        CancelPaymentUseCase cancelPayment,
        RefreshPaymentStatusUseCase refreshPaymentStatus,
        IConfiguration configuration)
    {
        _createPayment = createPayment;
        _getPayment = getPayment;
        _listPayments = listPayments;
        _refundPayment = refundPayment;
        _cancelPayment = cancelPayment;
        _refreshPaymentStatus = refreshPaymentStatus;
        _configuration = configuration;
    }

    /// <summary>Charges an order through the selected provider.</summary>
    /// <param name="request">Payment data.</param>
    /// <param name="idempotencyKey">Optional key that prevents duplicated charges.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created payment.</returns>
    /// <response code="201">Payment created.</response>
    /// <response code="400">The request violates a business rule.</response>
    /// <response code="404">The order does not exist.</response>
    /// <response code="409">The order is not confirmed.</response>
    /// <response code="502">The provider call failed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentDto>> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreatePaymentCommand(
            request.OrderId,
            request.Provider,
            request.Method,
            request.Amount,
            request.Description,
            request.CustomerEmail,
            request.CustomerName,
            request.CustomerDocument,
            request.CardToken,
            request.Installments,
            request.SimulateFailure,
            BuildCallbackUrl(request.Provider),
            idempotencyKey);

        var payment = await _createPayment.ExecuteAsync(command, cancellationToken);

        return CreatedAtAction(nameof(GetPaymentById), new { id = payment.Id }, payment);
    }

    /// <summary>Returns a single payment.</summary>
    /// <param name="id">Payment identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payment.</returns>
    /// <response code="200">Payment found.</response>
    /// <response code="404">Payment not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentDto>> GetPaymentById(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _getPayment.ExecuteAsync(id, cancellationToken);
        return Ok(payment);
    }

    /// <summary>Lists payments with pagination and optional filters.</summary>
    /// <param name="orderId">Optional order filter.</param>
    /// <param name="provider">Optional provider filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="size">Page size (1..100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of payments.</returns>
    /// <response code="200">Page returned.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PaymentDto>>> ListPayments(
        [FromQuery] Guid? orderId,
        [FromQuery] PaymentProvider? provider,
        [FromQuery] PaymentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _listPayments.ExecuteAsync(
            new ListPaymentsQuery(page, size, orderId, provider, status),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Refunds a captured payment.</summary>
    /// <param name="id">Payment identity.</param>
    /// <param name="request">Amount to refund (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refunded payment.</returns>
    /// <response code="200">Payment refunded.</response>
    /// <response code="404">Payment not found.</response>
    /// <response code="409">The payment cannot be refunded.</response>
    /// <response code="502">The provider refused the refund.</response>
    [HttpPost("{id:guid}/refund")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentDto>> RefundPayment(
        Guid id,
        [FromBody] RefundPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        var payment = await _refundPayment.ExecuteAsync(id, request?.Amount, cancellationToken);
        return Ok(payment);
    }

    /// <summary>Cancels a payment that has not been captured yet.</summary>
    /// <param name="id">Payment identity.</param>
    /// <param name="request">Cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancelled payment.</returns>
    /// <response code="200">Payment cancelled.</response>
    /// <response code="400">The reason is missing.</response>
    /// <response code="404">Payment not found.</response>
    /// <response code="409">The payment cannot be cancelled.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentDto>> CancelPayment(
        Guid id,
        [FromBody] CancelPaymentRequest? request,
        CancellationToken cancellationToken)
    {
        var payment = await _cancelPayment.ExecuteAsync(id, request?.Reason ?? string.Empty, cancellationToken);
        return Ok(payment);
    }

    /// <summary>Asks the provider for the current status and syncs the payment.</summary>
    /// <param name="id">Payment identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refreshed payment.</returns>
    /// <response code="200">Payment refreshed.</response>
    /// <response code="404">Payment not found.</response>
    /// <response code="409">The payment was never sent to the provider.</response>
    /// <response code="502">The provider call failed.</response>
    [HttpPost("{id:guid}/refresh")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentDto>> RefreshPaymentStatus(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _refreshPaymentStatus.ExecuteAsync(id, cancellationToken);
        return Ok(payment);
    }

    private string? BuildCallbackUrl(PaymentProvider provider)
    {
        var baseUrl = _configuration["Payments:CallbackUrl"];

        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : $"{baseUrl.TrimEnd('/')}/{provider.ToString().ToLowerInvariant()}";
    }
}
