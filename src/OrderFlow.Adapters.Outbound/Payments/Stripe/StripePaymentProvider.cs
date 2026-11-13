using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Adapters.Outbound.Payments.Stripe;

/// <summary>
/// Stripe payment provider adapter.
/// </summary>
/// <remarks>
/// <para>
/// Cards are charged with a PaymentIntent (a test payment method is used, so the
/// sandbox answers immediately). Pix and Boleto use a Checkout Session, because
/// those instruments require an extra confirmation step in the browser — the
/// session URL is returned to the caller.
/// </para>
/// <para>
/// Stripe identifies its objects by prefix (<c>pi_…</c>, <c>cs_…</c>), so the
/// adapter knows which endpoint to call without storing extra state.
/// </para>
/// </remarks>
public sealed class StripePaymentProvider : IPaymentProvider
{
    private const string ProviderName = "Stripe";

    private readonly HttpClient _client;
    private readonly StripeOptions _options;

    /// <summary>Creates the adapter.</summary>
    /// <param name="client">Configured HTTP client.</param>
    /// <param name="options">Adapter options.</param>
    public StripePaymentProvider(HttpClient client, IOptions<StripeOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    /// <inheritdoc />
    public PaymentProvider Provider => PaymentProvider.Stripe;

    /// <inheritdoc />
    public string DisplayName => ProviderName;

    /// <inheritdoc />
    public bool IsSimulation => false;

    /// <inheritdoc />
    public bool IsConfigured => _options.IsConfigured;

    /// <inheritdoc />
    public IReadOnlyCollection<PaymentMethod> SupportedMethods =>
        PaymentPolicy.MethodsFor(PaymentProvider.Stripe);

    /// <inheritdoc />
    public async Task<ProviderChargeResult> ChargeAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConfigured();

        return request.Method is PaymentMethod.CreditCard or PaymentMethod.DebitCard
            ? await CreatePaymentIntentAsync(request, cancellationToken)
            : await CreateCheckoutSessionAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProviderChargeResult> GetChargeAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (IsCheckoutSession(providerPaymentId))
        {
            var payload = await GetAsync($"/v1/checkout/sessions/{providerPaymentId}", cancellationToken);
            var json = ProviderHttp.ParseJson(payload, ProviderName);

            return new ProviderChargeResult(
                providerPaymentId,
                MapCheckoutStatus(ProviderHttp.GetString(json, "payment_status")),
                ProviderHttp.GetString(json, "payment_status") ?? ProviderHttp.GetString(json, "status"),
                ProviderHttp.GetString(json, "url"),
                RawResponse: payload);
        }

        var intentPayload = await GetAsync($"/v1/payment_intents/{providerPaymentId}", cancellationToken);
        var intentJson = ProviderHttp.ParseJson(intentPayload, ProviderName);
        var status = ProviderHttp.GetString(intentJson, "status") ?? "unknown";

        return new ProviderChargeResult(
            providerPaymentId,
            MapIntentStatus(status),
            status,
            RawResponse: intentPayload);
    }

    /// <inheritdoc />
    public async Task<ProviderRefundResult> RefundAsync(
        string providerPaymentId,
        Money amount,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentNullException.ThrowIfNull(amount);

        var paymentIntent = providerPaymentId;
        if (IsCheckoutSession(providerPaymentId))
        {
            var sessionPayload = await GetAsync($"/v1/checkout/sessions/{providerPaymentId}", cancellationToken);
            var sessionJson = ProviderHttp.ParseJson(sessionPayload, ProviderName);
            paymentIntent = ProviderHttp.GetString(sessionJson, "payment_intent") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(paymentIntent))
            {
                throw new PaymentProviderException(
                    "The Stripe checkout session has no payment intent to refund.",
                    sessionPayload);
            }
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("payment_intent", paymentIntent),
            new("amount", ToCents(amount.Amount).ToString(CultureInfo.InvariantCulture))
        };

        var request = ProviderHttp.CreateFormRequest(HttpMethod.Post, "/v1/refunds", form);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = await ProviderHttp.SendAsync(_client, request, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);
        var status = ProviderHttp.GetString(json, "status") ?? "unknown";

        return new ProviderRefundResult(
            string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase),
            ProviderHttp.GetString(json, "id"),
            status,
            FailureReason: string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase)
                ? null
                : $"Stripe refused the refund (status: {status}).",
            RawResponse: payload);
    }

    /// <inheritdoc />
    public PaymentStatus MapStatus(string providerStatus)
    {
        return providerStatus.ToLowerInvariant() switch
        {
            "succeeded" or "paid" or "complete" => PaymentStatus.Paid,
            "processing" or "requires_capture" or "requires_action" or "requires_confirmation" or "unpaid" =>
                PaymentStatus.Processing,
            "refunded" => PaymentStatus.Refunded,
            _ => PaymentStatus.Failed
        };
    }

    private async Task<ProviderChargeResult> CreatePaymentIntentAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("amount", ToCents(request.Amount.Amount).ToString(CultureInfo.InvariantCulture)),
            new("currency", _options.Currency.ToLowerInvariant()),
            new("payment_method_types[]", "card"),
            new("payment_method", _options.CardPaymentMethod),
            new("confirm", "true"),
            new("description", request.Description)
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            form.Add(new KeyValuePair<string, string>("receipt_email", request.CustomerEmail!));
        }

        form.AddRange(MetadataFields(request.Metadata));

        var httpRequest = ProviderHttp.CreateFormRequest(HttpMethod.Post, "/v1/payment_intents", form);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        var id = ProviderHttp.GetString(json, "id")
            ?? throw new PaymentProviderException("Stripe did not return a payment identifier.", payload);

        var status = ProviderHttp.GetString(json, "status") ?? "unknown";

        return new ProviderChargeResult(
            id,
            MapIntentStatus(status),
            status,
            FailureReason: MapIntentStatus(status) == PaymentStatus.Failed
                ? $"Stripe declined the charge (status: {status})."
                : null,
            RawResponse: payload);
    }

    private async Task<ProviderChargeResult> CreateCheckoutSessionAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        var paymentMethodType = request.Method is PaymentMethod.Pix ? "pix" : "boleto";

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("payment_method_types[]", paymentMethodType),
            new("line_items[0][price_data][currency]", _options.Currency.ToLowerInvariant()),
            new("line_items[0][price_data][unit_amount]", ToCents(request.Amount.Amount).ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][price_data][product_data][name]", request.Description),
            new("line_items[0][quantity]", "1"),
            new("success_url", _options.SuccessUrl),
            new("cancel_url", _options.CancelUrl),
            new("client_reference_id", request.OrderId.ToString("N"))
        };

        form.AddRange(MetadataFields(request.Metadata));

        var httpRequest = ProviderHttp.CreateFormRequest(HttpMethod.Post, "/v1/checkout/sessions", form);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        var id = ProviderHttp.GetString(json, "id")
            ?? throw new PaymentProviderException("Stripe did not return a checkout session identifier.", payload);

        var paymentStatus = ProviderHttp.GetString(json, "payment_status") ?? "unpaid";

        return new ProviderChargeResult(
            id,
            MapCheckoutStatus(paymentStatus),
            paymentStatus,
            ProviderHttp.GetString(json, "url"),
            RawResponse: payload);
    }

    private async Task<string> GetAsync(string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return await ProviderHttp.SendAsync(_client, request, ProviderName, cancellationToken);
    }

    private static IEnumerable<KeyValuePair<string, string>> MetadataFields(IReadOnlyDictionary<string, string> metadata)
    {
        foreach (var entry in metadata)
        {
            yield return new KeyValuePair<string, string>($"metadata[{entry.Key}]", entry.Value);
        }
    }

    private static bool IsCheckoutSession(string providerPaymentId)
    {
        return providerPaymentId.StartsWith("cs_", StringComparison.Ordinal);
    }

    private static PaymentStatus MapIntentStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "succeeded" => PaymentStatus.Paid,
            "processing" or "requires_capture" or "requires_action" or "requires_confirmation" =>
                PaymentStatus.Processing,
            _ => PaymentStatus.Failed
        };
    }

    private static PaymentStatus MapCheckoutStatus(string? paymentStatus)
    {
        return paymentStatus?.ToLowerInvariant() switch
        {
            "paid" or "no_payment_required" => PaymentStatus.Paid,
            "unpaid" => PaymentStatus.Pending,
            _ => PaymentStatus.Processing
        };
    }

    private static long ToCents(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new PaymentProviderException(
                "Stripe is not configured. Set 'Payments:Stripe:ApiKey' (use a test key such as 'sk_test_…').");
        }
    }
}
