using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Application.Payments.ApplyProviderCallback;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Adapters.Inbound.Api;

/// <summary>
/// Inbound HTTP adapter that receives the status notifications sent by the
/// payment providers.
/// </summary>
/// <remarks>
/// Each provider has its own payload shape, so the controller only extracts the
/// payment reference and the raw status; the translation to the domain
/// vocabulary is done by the provider adapter (<c>IPaymentProvider.MapStatus</c>).
/// </remarks>
[ApiController]
[Route("api/webhooks")]
[Produces("application/json")]
public sealed class ProviderWebhookController : ControllerBase
{
    private readonly ApplyProviderCallbackUseCase _applyCallback;

    /// <summary>Creates the controller.</summary>
    /// <param name="applyCallback">Apply-callback use case.</param>
    public ProviderWebhookController(ApplyProviderCallbackUseCase applyCallback)
    {
        _applyCallback = applyCallback;
    }

    /// <summary>Receives a provider notification.</summary>
    /// <param name="provider">Provider that sent the notification.</param>
    /// <param name="payload">Raw provider payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the callback.</returns>
    /// <response code="200">Notification processed (or ignored).</response>
    [HttpPost("{provider}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Handle(
        PaymentProvider provider,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var (providerPaymentId, providerStatus) = Extract(provider, payload);

        if (providerPaymentId is null || providerStatus is null)
        {
            return Ok(new { received = true, applied = false, reason = "Unsupported payload." });
        }

        var applied = await _applyCallback.ExecuteAsync(
            new ProviderCallbackCommand(provider, providerPaymentId, providerStatus),
            cancellationToken);

        return Ok(new { received = true, applied });
    }

    private static (string? ProviderPaymentId, string? ProviderStatus) Extract(
        PaymentProvider provider,
        JsonElement payload)
    {
        return provider switch
        {
            // Stripe: { "data": { "object": { "id": "pi_…", "status": "succeeded" } } }
            PaymentProvider.Stripe => (
                ReadString(payload, "data", "object", "id") ?? ReadString(payload, "id"),
                ReadString(payload, "data", "object", "status") ?? ReadString(payload, "status")),

            // Mercado Pago: { "id": 12345, "status": "approved" }
            PaymentProvider.MercadoPago => (
                ReadString(payload, "id") ?? ReadString(payload, "data", "id"),
                ReadString(payload, "status") ?? ReadString(payload, "data", "status")),

            // Asaas: { "payment": { "id": "pay_…", "status": "CONFIRMED" } }
            PaymentProvider.Asaas => (
                ReadString(payload, "payment", "id") ?? ReadString(payload, "id"),
                ReadString(payload, "payment", "status") ?? ReadString(payload, "status")),

            // Simulator: { "id": "sim_…", "status": "approved" }
            PaymentProvider.Simulated => (
                ReadString(payload, "id"),
                ReadString(payload, "status")),

            _ => (null, null)
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
