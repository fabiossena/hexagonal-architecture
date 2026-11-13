using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Payments.ApplyProviderCallback;
using OrderFlow.Application.Payments.CreatePayment;
using OrderFlow.Application.Payments.GetPayment;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Translates the iAppForms checkout surface into OrderFlow use cases and back.
/// </summary>
/// <remarks>
/// <para>
/// This is the anti-corruption layer of the integration: it understands the
/// iAppForms vocabulary (quote ids, method codes, commercial statuses) and
/// delegates everything else to the application layer.
/// </para>
/// <para>
/// The quote is an OrderFlow <b>order</b>: the integration creates it with the
/// quote id as <c>externalReference</c>, and this service finds it back by that
/// reference.
/// </para>
/// </remarks>
public sealed class IAppFormsPaymentSurface
{
    private readonly IOrderRepository _orders;
    private readonly CreatePaymentUseCase _createPayment;
    private readonly GetPaymentUseCase _getPayment;
    private readonly ApplyProviderCallbackUseCase _applyCallback;
    private readonly IPaymentProviderResolver _providers;
    private readonly IOptions<IAppFormsOptions> _options;

    /// <summary>Creates the service.</summary>
    /// <param name="orders">Order repository port.</param>
    /// <param name="createPayment">Create-payment use case.</param>
    /// <param name="getPayment">Get-payment use case.</param>
    /// <param name="applyCallback">Apply-provider-callback use case.</param>
    /// <param name="providers">Provider resolver port.</param>
    /// <param name="options">Integration options.</param>
    public IAppFormsPaymentSurface(
        IOrderRepository orders,
        CreatePaymentUseCase createPayment,
        GetPaymentUseCase getPayment,
        ApplyProviderCallbackUseCase applyCallback,
        IPaymentProviderResolver providers,
        IOptions<IAppFormsOptions> options)
    {
        _orders = orders;
        _createPayment = createPayment;
        _getPayment = getPayment;
        _applyCallback = applyCallback;
        _providers = providers;
        _options = options;
    }

    /// <summary>Charges the quote and returns the iAppForms surface answer.</summary>
    /// <param name="request">iAppForms payment request.</param>
    /// <param name="idempotencyKey">Key sent in the <c>Idempotency-Key</c> header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payment as the iAppForms surface sees it.</returns>
    /// <exception cref="CheckoutException">The quote, the method or the provider is not usable.</exception>
    public async Task<PaymentSurfaceDto> CreateAsync(
        PaymentSurfaceRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await _orders.GetByExternalReferenceAsync(request.QuoteId.ToString(), cancellationToken)
            ?? throw new CheckoutException(
                StatusCodes.Status404NotFound,
                CheckoutErrorCodes.QuoteNotFound,
                $"Quote '{request.QuoteId}' was not found.");

        if (!IAppFormsVocabulary.TryParseMethod(request.PaymentMethodCode, out var method))
        {
            throw new CheckoutException(
                StatusCodes.Status400BadRequest,
                CheckoutErrorCodes.UnsupportedCombination,
                $"Payment method '{request.PaymentMethodCode}' is not supported.");
        }

        var provider = ResolveProvider(method);

        var command = new CreatePaymentCommand(
            order.Id,
            provider,
            method,
            order.Total.Amount,
            $"iAppForms quote {request.QuoteId:N}",
            CustomerEmail: null,
            CustomerName: null,
            CustomerDocument: null,
            CardToken: null,
            Installments: 1,
            SimulateFailure: false,
            CallbackUrl: null,
            IdempotencyKey: idempotencyKey);

        var payment = await _createPayment.ExecuteAsync(command, cancellationToken);

        return ToSurface(payment);
    }

    /// <summary>Returns the current state of a payment in the iAppForms vocabulary.</summary>
    /// <param name="paymentId">Payment identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payment status as the iAppForms surface sees it.</returns>
    /// <exception cref="CheckoutException">The payment does not exist.</exception>
    public async Task<PaymentStatusDto> GetStatusAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _getPayment.ExecuteAsync(paymentId, cancellationToken);

        return new PaymentStatusDto(
            payment.Id,
            IAppFormsVocabulary.ToPlanPaymentStatus(
                ParseStatus(payment.Status),
                !string.IsNullOrWhiteSpace(payment.ProviderPaymentId)),
            IAppFormsVocabulary.ToProviderStatus(ParseStatus(payment.Status)),
            payment.Provider,
            IAppFormsVocabulary.ToMethodCode(ParseMethod(payment.Method)),
            payment.Amount.Amount,
            payment.Amount.Currency,
            payment.CheckoutUrl,
            BuildInstructions(payment),
            SupportedOperations(ParseStatus(payment.Status)),
            payment.CreatedAtUtc);
    }

    /// <summary>Applies a provider notification to the matching payment.</summary>
    /// <param name="providerName">Adapter name that sent the notification.</param>
    /// <param name="providerPaymentId">Provider reference.</param>
    /// <param name="providerStatus">Raw status reported by the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a payment was updated.</returns>
    public async Task<bool> ApplyWebhookAsync(
        string providerName,
        string providerPaymentId,
        string providerStatus,
        CancellationToken cancellationToken = default)
    {
        if (!IAppFormsVocabulary.TryParseProvider(providerName, out var provider))
        {
            throw new CheckoutException(
                StatusCodes.Status404NotFound,
                CheckoutErrorCodes.ProviderUnavailable,
                $"Unknown provider '{providerName}'.");
        }

        return await _applyCallback.ExecuteAsync(
            new ProviderCallbackCommand(provider, providerPaymentId, providerStatus),
            cancellationToken);
    }

    /// <summary>Checks the shared secret sent by the caller.</summary>
    /// <param name="apiKey">Value of the <c>X-Api-Key</c> header.</param>
    /// <exception cref="CheckoutException">The surface is disabled or the key does not match.</exception>
    public void EnsureAuthorized(string? apiKey)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            throw new CheckoutException(
                StatusCodes.Status404NotFound,
                CheckoutErrorCodes.Unauthorized,
                "The iAppForms compatibility surface is disabled.");
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey) &&
            !string.Equals(options.ApiKey, apiKey, StringComparison.Ordinal))
        {
            throw new CheckoutException(
                StatusCodes.Status401Unauthorized,
                CheckoutErrorCodes.Unauthorized,
                "A valid X-Api-Key header is required.");
        }
    }

    /// <summary>Chooses the provider that will serve the payment.</summary>
    /// <param name="method">Payment instrument.</param>
    /// <returns>The provider to charge.</returns>
    /// <exception cref="CheckoutException">No provider is able to serve the method.</exception>
    private PaymentProvider ResolveProvider(PaymentMethod method)
    {
        var options = _options.Value;
        var methodCode = IAppFormsVocabulary.ToMethodCode(method);

        var preference = options.ProviderPreferenceByMethod.TryGetValue(methodCode, out var configured)
            ? configured
            : Array.Empty<string>();

        foreach (var candidate in preference)
        {
            if (!IAppFormsVocabulary.TryParseProvider(candidate, out var provider))
            {
                continue;
            }

            var adapter = _providers.Describe().FirstOrDefault(item => item.Provider == provider);

            if (adapter is null || !adapter.Methods.Contains(method))
            {
                continue;
            }

            if (adapter.IsConfigured || adapter.IsSimulation)
            {
                return provider;
            }
        }

        if (!string.IsNullOrWhiteSpace(options.DefaultProvider) &&
            IAppFormsVocabulary.TryParseProvider(options.DefaultProvider, out var fallback))
        {
            return fallback;
        }

        throw new CheckoutException(
            StatusCodes.Status502BadGateway,
            CheckoutErrorCodes.ProviderUnavailable,
            $"No configured provider can serve '{methodCode}'.");
    }

    private static PaymentSurfaceDto ToSurface(PaymentDto payment)
    {
        var status = ParseStatus(payment.Status);

        return new PaymentSurfaceDto(
            payment.Id,
            IAppFormsVocabulary.ToPlanPaymentStatus(status, !string.IsNullOrWhiteSpace(payment.ProviderPaymentId)),
            IAppFormsVocabulary.ToProviderStatus(status),
            payment.Provider,
            payment.CheckoutUrl,
            BuildInstructions(payment),
            payment.Reused);
    }

    private static ProviderInstructions? BuildInstructions(PaymentDto payment)
    {
        var hasInstructions = !string.IsNullOrWhiteSpace(payment.CheckoutUrl) ||
                              !string.IsNullOrWhiteSpace(payment.PixCopyPasteCode) ||
                              !string.IsNullOrWhiteSpace(payment.PixQrCodeBase64);

        if (!hasInstructions)
        {
            return null;
        }

        return new ProviderInstructions(
            IAppFormsVocabulary.ToMethodCode(ParseMethod(payment.Method)),
            payment.PixQrCodeBase64,
            payment.PixCopyPasteCode,
            Barcode: null,
            payment.CheckoutUrl);
    }

    private static IReadOnlyList<string> SupportedOperations(PaymentStatus status)
    {
        return status switch
        {
            PaymentStatus.Pending or PaymentStatus.Processing => new[] { "cancel" },
            PaymentStatus.Paid => new[] { "refund" },
            _ => Array.Empty<string>()
        };
    }

    private static PaymentStatus ParseStatus(string status)
    {
        return Enum.TryParse(status, ignoreCase: true, out PaymentStatus parsed)
            ? parsed
            : PaymentStatus.Pending;
    }

    private static PaymentMethod ParseMethod(string method)
    {
        return Enum.TryParse(method, ignoreCase: true, out PaymentMethod parsed)
            ? parsed
            : PaymentMethod.Pix;
    }
}
