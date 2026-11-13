using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Adapters.Outbound.Payments.MercadoPago;

/// <summary>
/// Mercado Pago payment provider adapter.
/// </summary>
/// <remarks>
/// A single endpoint (<c>POST /v1/payments</c>) covers every instrument: the
/// <c>payment_method_id</c> selects between card, Pix, boleto and bank transfer.
/// Cards additionally need a token produced by Mercado Pago's own SDK.
/// </remarks>
public sealed class MercadoPagoPaymentProvider : IPaymentProvider
{
    private const string ProviderName = "Mercado Pago";

    private readonly HttpClient _client;
    private readonly MercadoPagoOptions _options;

    /// <summary>Creates the adapter.</summary>
    /// <param name="client">Configured HTTP client.</param>
    /// <param name="options">Adapter options.</param>
    public MercadoPagoPaymentProvider(HttpClient client, IOptions<MercadoPagoOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    /// <inheritdoc />
    public PaymentProvider Provider => PaymentProvider.MercadoPago;

    /// <inheritdoc />
    public string DisplayName => ProviderName;

    /// <inheritdoc />
    public bool IsSimulation => false;

    /// <inheritdoc />
    public bool IsConfigured => _options.IsConfigured;

    /// <inheritdoc />
    public IReadOnlyCollection<PaymentMethod> SupportedMethods =>
        PaymentPolicy.MethodsFor(PaymentProvider.MercadoPago);

    /// <inheritdoc />
    public async Task<ProviderChargeResult> ChargeAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConfigured();

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["transaction_amount"] = request.Amount.Amount,
            ["description"] = request.Description,
            ["payment_method_id"] = MapMethod(request.Method),
            ["installments"] = request.Method is PaymentMethod.CreditCard or PaymentMethod.DebitCard
                ? request.Installments
                : 1,
            ["external_reference"] = request.OrderId.ToString("N"),
            ["notification_url"] = _options.NotificationUrl,
            ["statement_descriptor"] = _options.StatementDescriptor,
            ["metadata"] = request.Metadata
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            body["payer"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["email"] = request.CustomerEmail!
            };
        }

        if (!string.IsNullOrWhiteSpace(request.CardToken))
        {
            body["token"] = request.CardToken;
        }

        var httpRequest = ProviderHttp.CreateJsonRequest(HttpMethod.Post, "/v1/payments", body);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        httpRequest.Headers.TryAddWithoutValidation("X-Idempotency-Key", request.IdempotencyKey);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        var id = ProviderHttp.GetString(json, "id")
            ?? throw new PaymentProviderException("Mercado Pago did not return a payment identifier.", payload);

        var status = ProviderHttp.GetString(json, "status") ?? "unknown";
        var statusDetail = ProviderHttp.GetString(json, "status_detail");

        return new ProviderChargeResult(
            id,
            MapStatus(status),
            status,
            ProviderHttp.GetString(json, "transaction_details", "external_resource_url"),
            ProviderHttp.GetString(json, "point_of_interaction", "transaction_data", "qr_code"),
            ProviderHttp.GetString(json, "point_of_interaction", "transaction_data", "qr_code_base64"),
            MapStatus(status) == PaymentStatus.Failed
                ? $"Mercado Pago rejected the charge ({status}: {statusDetail ?? "no details"})."
                : null,
            payload);
    }

    /// <inheritdoc />
    public async Task<ProviderChargeResult> GetChargeAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/payments/{providerPaymentId}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        var status = ProviderHttp.GetString(json, "status") ?? "unknown";

        return new ProviderChargeResult(
            providerPaymentId,
            MapStatus(status),
            status,
            ProviderHttp.GetString(json, "transaction_details", "external_resource_url"),
            ProviderHttp.GetString(json, "point_of_interaction", "transaction_data", "qr_code"),
            ProviderHttp.GetString(json, "point_of_interaction", "transaction_data", "qr_code_base64"),
            RawResponse: payload);
    }

    /// <inheritdoc />
    public async Task<ProviderRefundResult> RefundAsync(
        string providerPaymentId,
        Money amount,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ArgumentNullException.ThrowIfNull(amount);

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["amount"] = amount.Amount
        };

        var httpRequest = ProviderHttp.CreateJsonRequest(
            HttpMethod.Post,
            $"/v1/payments/{providerPaymentId}/refunds",
            body);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        var status = ProviderHttp.GetString(json, "status") ?? "unknown";
        var approved = string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase);

        return new ProviderRefundResult(
            approved,
            ProviderHttp.GetString(json, "id"),
            status,
            FailureReason: approved ? null : $"Mercado Pago refused the refund (status: {status}).",
            RawResponse: payload);
    }

    /// <inheritdoc />
    public PaymentStatus MapStatus(string providerStatus)
    {
        return providerStatus.ToLowerInvariant() switch
        {
            "approved" => PaymentStatus.Paid,
            "authorized" or "in_process" or "pending" => PaymentStatus.Processing,
            "refunded" => PaymentStatus.Refunded,
            _ => PaymentStatus.Failed
        };
    }

    private static string MapMethod(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.CreditCard or PaymentMethod.DebitCard => "credit_card",
            PaymentMethod.Pix => "pix",
            PaymentMethod.Boleto => "boleto",
            PaymentMethod.BankTransfer => "bank_transfer",
            _ => throw new PaymentProviderException($"Unsupported method '{method}'.")
        };
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new PaymentProviderException(
                "Mercado Pago is not configured. Set 'Payments:MercadoPago:AccessToken' (use the test token 'APP_USR-…').");
        }
    }
}
