using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Inbound HTTP adapter that exposes the iAppForms checkout surface.
/// </summary>
/// <remarks>
/// <para>
/// The routes and payloads mirror the ones the iAppForms Authorization module
/// already publishes (<c>/api/checkout/payments</c> and
/// <c>/api/checkout/webhooks/{provider}</c>), so pointing the iAppForms
/// traffic here is a configuration change, not a rewrite.
/// </para>
/// <para>
/// The shared secret is read from the <c>X-Api-Key</c> header.
/// </para>
/// </remarks>
[ApiController]
[Route("api/checkout")]
[Produces("application/json")]
public sealed class CheckoutPaymentsController : ControllerBase
{
    private readonly IAppFormsPaymentSurface _surface;

    /// <summary>Creates the controller.</summary>
    /// <param name="surface">Checkout surface service.</param>
    public CheckoutPaymentsController(IAppFormsPaymentSurface surface)
    {
        _surface = surface;
    }

    /// <summary>Charges a quote.</summary>
    /// <param name="request">Payment request.</param>
    /// <param name="idempotencyKey">Key sent in the <c>Idempotency-Key</c> header.</param>
    /// <param name="apiKey">Shared secret sent in the <c>X-Api-Key</c> header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created payment.</returns>
    /// <response code="201">Payment created.</response>
    /// <response code="400">The method is not supported.</response>
    /// <response code="401">The API key is missing or invalid.</response>
    /// <response code="404">The quote does not exist.</response>
    /// <response code="502">No provider is able to serve the payment.</response>
    [HttpPost("payments")]
    [ProducesResponseType(typeof(PaymentSurfaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentSurfaceDto>> CreatePayment(
        [FromBody] PaymentSurfaceRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        _surface.EnsureAuthorized(apiKey);

        var payment = await _surface.CreateAsync(request, idempotencyKey, cancellationToken);

        return CreatedAtAction(nameof(GetPayment), new { id = payment.PaymentId }, payment);
    }

    /// <summary>Returns the current state of a payment.</summary>
    /// <param name="id">Payment identity.</param>
    /// <param name="apiKey">Shared secret sent in the <c>X-Api-Key</c> header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payment status.</returns>
    /// <response code="200">Payment found.</response>
    /// <response code="401">The API key is missing or invalid.</response>
    /// <response code="404">The payment does not exist.</response>
    [HttpGet("payments/{id:guid}")]
    [ProducesResponseType(typeof(PaymentStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentStatusDto>> GetPayment(
        Guid id,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        _surface.EnsureAuthorized(apiKey);

        var status = await _surface.GetStatusAsync(id, cancellationToken);
        return Ok(status);
    }
}
