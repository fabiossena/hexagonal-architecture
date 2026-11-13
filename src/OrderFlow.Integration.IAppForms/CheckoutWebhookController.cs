using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Inbound HTTP adapter that receives the provider notifications on the iAppForms
/// route (<c>/api/checkout/webhooks/{provider}</c>).
/// </summary>
/// <remarks>
/// The payload shapes are the ones the providers send; only the payment
/// reference and the raw status are extracted here — the translation to the
/// domain vocabulary is done by the provider adapter.
/// </remarks>
[ApiController]
[Route("api/checkout/webhooks")]
[Produces("application/json")]
public sealed class CheckoutWebhookController : ControllerBase
{
    private readonly IAppFormsPaymentSurface _surface;

    /// <summary>Creates the controller.</summary>
    /// <param name="surface">Checkout surface service.</param>
    public CheckoutWebhookController(IAppFormsPaymentSurface surface)
    {
        _surface = surface;
    }

    /// <summary>Receives a provider notification.</summary>
    /// <param name="provider">Adapter name that sent the notification.</param>
    /// <param name="payload">Raw provider payload.</param>
    /// <param name="apiKey">Shared secret sent in the <c>X-Api-Key</c> header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What happened to the notification.</returns>
    /// <response code="200">Notification processed (or ignored).</response>
    /// <response code="401">The API key is missing or invalid.</response>
    /// <response code="404">The provider is unknown.</response>
    [HttpPost("{provider}")]
    [ProducesResponseType(typeof(WebhookSurfaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookSurfaceDto>> Handle(
        string provider,
        [FromBody] JsonElement payload,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        _surface.EnsureAuthorized(apiKey);

        var (providerPaymentId, providerStatus) = Extract(provider, payload);

        if (providerPaymentId is null || providerStatus is null)
        {
            return Ok(new WebhookSurfaceDto("Skipped"));
        }

        var applied = await _surface.ApplyWebhookAsync(
            provider,
            providerPaymentId,
            providerStatus,
            cancellationToken);

        return Ok(new WebhookSurfaceDto(applied ? "Applied" : "UnknownPayment", providerPaymentId));
    }

    private static (string? ProviderPaymentId, string? ProviderStatus) Extract(
        string provider,
        JsonElement payload)
    {
        if (!IAppFormsVocabulary.TryParseProvider(provider, out var parsed))
        {
            return (null, null);
        }

        return parsed switch
        {
            // Stripe: { "data": { "object": { "id": "pi_…", "status": "succeeded" } } }
            Domain.Payments.PaymentProvider.Stripe => (
                ReadString(payload, "data", "object", "id") ?? ReadString(payload, "id"),
                ReadString(payload, "data", "object", "status") ?? ReadString(payload, "status")),

            // Mercado Pago: { "id": 12345, "status": "approved" }
            Domain.Payments.PaymentProvider.MercadoPago => (
                ReadString(payload, "id") ?? ReadString(payload, "data", "id"),
                ReadString(payload, "status") ?? ReadString(payload, "data", "status")),

            // Asaas: { "payment": { "id": "pay_…", "status": "CONFIRMED" } }
            Domain.Payments.PaymentProvider.Asaas => (
                ReadString(payload, "payment", "id") ?? ReadString(payload, "id"),
                ReadString(payload, "payment", "status") ?? ReadString(payload, "status")),

            // Simulator: { "id": "sim_…", "status": "approved" }
            _ => (
                ReadString(payload, "id"),
                ReadString(payload, "status"))
        };
    }

    private static string? ReadString(JsonElement payload, params string[] path)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var current = payload;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            _ => null
        };
    }
}
