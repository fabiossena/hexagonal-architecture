using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Adapters.Outbound.Payments.Asaas;

/// <summary>
/// Asaas payment provider adapter.
/// </summary>
/// <remarks>
/// Asaas requires a customer: when the request does not carry one, the adapter
/// creates it on demand (<c>POST /v3/customers</c>) using the e-mail and the
/// tax id sent by the caller. Cards need a token produced by Asaas' own SDK.
/// </remarks>
public sealed class AsaasPaymentProvider : IPaymentProvider
{
    private const string ProviderName = "Asaas";

    private readonly HttpClient _client;
    private readonly AsaasOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the adapter.</summary>
    /// <param name="client">Configured HTTP client.</param>
    /// <param name="options">Adapter options.</param>
    /// <param name="timeProvider">Clock used to compute the due date.</param>
    public AsaasPaymentProvider(HttpClient client, IOptions<AsaasOptions> options, TimeProvider timeProvider)
    {
        _client = client;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public PaymentProvider Provider => PaymentProvider.Asaas;

    /// <inheritdoc />
    public string DisplayName => ProviderName;

    /// <inheritdoc />
    public bool IsSimulation => false;

    /// <inheritdoc />
    public bool IsConfigured => _options.IsConfigured;

    /// <inheritdoc />
    public IReadOnlyCollection<PaymentMethod> SupportedMethods =>
        PaymentPolicy.MethodsFor(PaymentProvider.Asaas);

    /// <inheritdoc />
    public async Task<ProviderChargeResult> ChargeAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConfigured();

        var customerId = await ResolveCustomerIdAsync(request, cancellationToken);

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["customer"] = customerId,
            ["billingType"] = MapBillingType(request.Method),
            ["value"] = request.Amount.Amount,
            ["dueDate"] = _timeProvider.GetUtcNow().UtcDateTime.AddDays(_options.DueDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["description"] = request.Description,
            ["externalId"] = request.OrderId.ToString("N"),
            ["remoteIp"] = "127.0.0.1",
            ["metadata"] = request.Metadata
        };

        if (request.Method is PaymentMethod.CreditCard or PaymentMethod.DebitCard)
        {
            var creditCard = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["installments"] = request.Installments
            };

            if (!string.IsNullOrWhiteSpace(request.CardToken))
            {
                creditCard["token"] = request.CardToken;
            }

            body["creditCard"] = creditCard;
        }

        var httpRequest = ProviderHttp.CreateJsonRequest(HttpMethod.Post, "/payments", body);
        ApplyAuthentication(httpRequest);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        var id = ProviderHttp.GetString(json, "id")
            ?? throw new PaymentProviderException("Asaas did not return a payment identifier.", payload);

        var status = ProviderHttp.GetString(json, "status") ?? "unknown";

        return new ProviderChargeResult(
            id,
            MapStatus(status),
            status,
            ProviderHttp.GetString(json, "bankSlipUrl") ?? ProviderHttp.GetString(json, "paymentUrl"),
            ProviderHttp.GetString(json, "pixQrCode"),
            ProviderHttp.GetString(json, "encodedImage"),
            MapStatus(status) == PaymentStatus.Failed
                ? $"Asaas rejected the charge (status: {status})."
                : null,
            payload);
    }

    /// <inheritdoc />
    public async Task<ProviderChargeResult> GetChargeAsync(
        string providerPaymentId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/payments/{providerPaymentId}");
        ApplyAuthentication(httpRequest);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        var status = ProviderHttp.GetString(json, "status") ?? "unknown";

        return new ProviderChargeResult(
            providerPaymentId,
            MapStatus(status),
            status,
            ProviderHttp.GetString(json, "bankSlipUrl") ?? ProviderHttp.GetString(json, "paymentUrl"),
            ProviderHttp.GetString(json, "pixQrCode"),
            ProviderHttp.GetString(json, "encodedImage"),
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

        var httpRequest = ProviderHttp.CreateJsonRequest(
            HttpMethod.Post,
            $"/payments/{providerPaymentId}/refund",
            new Dictionary<string, object?>(StringComparer.Ordinal));
        ApplyAuthentication(httpRequest);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        var status = ProviderHttp.GetString(json, "status") ?? "unknown";
        var confirmed = string.Equals(status, "CONFIRMED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "REFUNDED", StringComparison.OrdinalIgnoreCase);

        return new ProviderRefundResult(
            confirmed,
            ProviderHttp.GetString(json, "id"),
            status,
            FailureReason: confirmed ? null : $"Asaas refused the refund (status: {status}).",
            RawResponse: payload);
    }

    /// <inheritdoc />
    public PaymentStatus MapStatus(string providerStatus)
    {
        return providerStatus.ToUpperInvariant() switch
        {
            "CONFIRMED" or "RECEIVED" => PaymentStatus.Paid,
            "PENDING" => PaymentStatus.Processing,
            "REFUNDED" => PaymentStatus.Refunded,
            _ => PaymentStatus.Failed
        };
    }

    private async Task<string> ResolveCustomerIdAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateCustomer)
        {
            throw new PaymentProviderException(
                "Asaas requires a customer. Enable 'Payments:Asaas:AutoCreateCustomer' or send an existing customer id.");
        }

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = string.IsNullOrWhiteSpace(request.CustomerName) ? "OrderFlow customer" : request.CustomerName,
            ["email"] = request.CustomerEmail
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerDocument))
        {
            body["cpfCnpj"] = request.CustomerDocument;
        }

        var httpRequest = ProviderHttp.CreateJsonRequest(HttpMethod.Post, "/customers", body);
        ApplyAuthentication(httpRequest);

        var payload = await ProviderHttp.SendAsync(_client, httpRequest, ProviderName, cancellationToken);
        var json = ProviderHttp.ParseJson(payload, ProviderName);

        return ProviderHttp.GetString(json, "id")
            ?? throw new PaymentProviderException("Asaas did not return a customer identifier.", payload);
    }

    private void ApplyAuthentication(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-ASAAS-ACCESS-KEY", _options.AccessKey);
    }

    private static string MapBillingType(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.CreditCard or PaymentMethod.DebitCard => "CREDIT_CARD",
            PaymentMethod.Pix => "PIX",
            PaymentMethod.Boleto => "BOLETO",
            _ => throw new PaymentProviderException($"Unsupported method '{method}'.")
        };
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new PaymentProviderException(
                "Asaas is not configured. Set 'Payments:Asaas:AccessKey' (use the sandbox key '$sandbox_access_token_…').");
        }
    }
}
