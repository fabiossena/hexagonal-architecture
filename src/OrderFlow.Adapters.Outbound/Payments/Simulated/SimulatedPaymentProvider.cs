using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.Policies;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Adapters.Outbound.Payments.Simulated;

/// <summary>
/// Built-in simulator that answers like a payment provider without calling one.
/// </summary>
/// <remarks>
/// <para>
/// It exists so the whole flow — API, persistence and the test screen — can be
/// exercised without any credential. Cards are confirmed immediately, while Pix,
/// Boleto and bank transfer stay pending and are confirmed after
/// <see cref="SimulatedOptions.AutoPayAfterSeconds"/>, which is what the
/// "refresh status" button observes.
/// </para>
/// <para>
/// The test screen can also ask for a failure (the <c>simulate_failure</c>
/// metadata), so the error path is reachable too.
/// </para>
/// </remarks>
public sealed class SimulatedPaymentProvider : IPaymentProvider
{
    private const string ProviderName = "Simulated";

    private readonly ConcurrentDictionary<string, SimulatedCharge> _charges = new(StringComparer.Ordinal);
    private readonly SimulatedOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the simulator.</summary>
    /// <param name="options">Simulator options.</param>
    /// <param name="timeProvider">Clock.</param>
    public SimulatedPaymentProvider(IOptions<SimulatedOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public PaymentProvider Provider => PaymentProvider.Simulated;

    /// <inheritdoc />
    public string DisplayName => "Simulated";

    /// <inheritdoc />
    public bool IsSimulation => true;

    /// <inheritdoc />
    public bool IsConfigured => _options.IsConfigured;

    /// <inheritdoc />
    public IReadOnlyCollection<PaymentMethod> SupportedMethods =>
        PaymentPolicy.MethodsFor(PaymentProvider.Simulated);

    /// <inheritdoc />
    public Task<ProviderChargeResult> ChargeAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = $"sim_{Guid.NewGuid():N}";
        var nowUtc = _timeProvider.GetUtcNow();

        var shouldFail = request.Metadata.TryGetValue("simulate_failure", out var flag)
            && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);

        if (shouldFail)
        {
            var failure = new ProviderChargeResult(
                id,
                PaymentStatus.Failed,
                "rejected",
                FailureReason: "Simulated failure requested by the test screen.",
                RawResponse: BuildRawPayload(id, "rejected", request));

            _charges[id] = new SimulatedCharge(failure, nowUtc);
            return Task.FromResult(failure);
        }

        ProviderChargeResult result = request.Method is PaymentMethod.CreditCard or PaymentMethod.DebitCard
            ? new ProviderChargeResult(
                id,
                PaymentStatus.Paid,
                "approved",
                RawResponse: BuildRawPayload(id, "approved", request))
            : BuildPendingResult(id, request);

        _charges[id] = new SimulatedCharge(result, nowUtc);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<ProviderChargeResult> GetChargeAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        if (!_charges.TryGetValue(providerPaymentId, out var charge))
        {
            return Task.FromResult(new ProviderChargeResult(
                providerPaymentId,
                PaymentStatus.Failed,
                "unknown",
                FailureReason: "The simulator does not know this payment."));
        }

        var elapsed = _timeProvider.GetUtcNow() - charge.CreatedAtUtc;

        if (charge.Result.Status == PaymentStatus.Processing &&
            elapsed.TotalSeconds >= _options.AutoPayAfterSeconds)
        {
            var paid = charge.Result with
            {
                Status = PaymentStatus.Paid,
                ProviderStatus = "approved"
            };

            _charges[providerPaymentId] = new SimulatedCharge(paid, charge.CreatedAtUtc);
            return Task.FromResult(paid);
        }

        return Task.FromResult(charge.Result);
    }

    /// <inheritdoc />
    public Task<ProviderRefundResult> RefundAsync(
        string providerPaymentId,
        Money amount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!_charges.TryGetValue(providerPaymentId, out var charge))
        {
            return Task.FromResult(new ProviderRefundResult(
                false,
                null,
                "unknown",
                "The simulator does not know this payment."));
        }

        var refunded = charge.Result with
        {
            Status = PaymentStatus.Refunded,
            ProviderStatus = "refunded"
        };

        _charges[providerPaymentId] = new SimulatedCharge(refunded, charge.CreatedAtUtc);

        return Task.FromResult(new ProviderRefundResult(
            true,
            $"re_{Guid.NewGuid():N}",
            "refunded",
            RawResponse: $"{{\"id\":\"{providerPaymentId}\",\"status\":\"refunded\",\"amount\":{amount.Amount.ToString(CultureInfo.InvariantCulture)}}}"));
    }

    /// <inheritdoc />
    public PaymentStatus MapStatus(string providerStatus)
    {
        return providerStatus.ToLowerInvariant() switch
        {
            "approved" or "paid" => PaymentStatus.Paid,
            "pending" or "processing" => PaymentStatus.Processing,
            "refunded" => PaymentStatus.Refunded,
            _ => PaymentStatus.Failed
        };
    }

    private ProviderChargeResult BuildPendingResult(string id, ProviderChargeRequest request)
    {
        string? pixCode = null;
        string? pixQrCode = null;
        string? checkoutUrl = null;

        if (request.Method is PaymentMethod.Pix)
        {
            pixCode = BuildPixCode(request);
            pixQrCode = BuildQrCodeImage(pixCode);
        }
        else if (request.Method is PaymentMethod.Boleto)
        {
            checkoutUrl = $"https://simulated.local/boleto/{id}";
        }

        return new ProviderChargeResult(
            id,
            PaymentStatus.Processing,
            "pending",
            checkoutUrl,
            pixCode,
            pixQrCode,
            RawResponse: BuildRawPayload(id, "pending", request));
    }

    private static string BuildPixCode(ProviderChargeRequest request)
    {
        var value = request.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var key = request.IdempotencyKey.Length >= 4 ? request.IdempotencyKey[..4] : request.IdempotencyKey;

        return $"00020126580014BR.GOV.BCB.PIX0136{key}52040000530398654{value.Length:02}5802BR5913ORDERFLOW6009SAO PAULO62070503***6304ABCD";
    }

    /// <summary>
    /// Builds a QR-code *placeholder* (an SVG grid) for the simulated Pix charge.
    /// It is not a scannable BR Code — the simulator never moves real money.
    /// </summary>
    private static string BuildQrCodeImage(string payload)
    {
        var cells = new StringBuilder();
        var seed = payload.Aggregate(17, (accumulator, character) => accumulator * 31 + character);

        for (var row = 0; row < 15; row++)
        {
            for (var column = 0; column < 15; column++)
            {
                seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF;
                if ((seed >> 8) % 100 < 45)
                {
                    cells.Append(CultureInfo.InvariantCulture, $"M{column * 14} {row * 14}h14v14h-14z");
                }
            }
        }

        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='210' height='210' viewBox='0 0 210 210'>" +
            $"<rect width='210' height='210' fill='#fff'/><path fill='#0f172a' d='{cells}'/></svg>";

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }

    private static string BuildRawPayload(string id, string status, ProviderChargeRequest request)
    {
        var amount = request.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture);

        return $"{{\"id\":\"{id}\",\"status\":\"{status}\",\"amount\":{amount}," +
            $"\"currency\":\"{request.Amount.Currency}\",\"method\":\"{request.Method}\"," +
            $"\"order_id\":\"{request.OrderId:N}\",\"simulated\":true}}";
    }

    private sealed record SimulatedCharge(ProviderChargeResult Result, DateTimeOffset CreatedAtUtc);
}
