namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Structured error codes of the iAppForms checkout surface.
/// </summary>
/// <remarks>
/// The codes are copied verbatim so the iAppForms front-end can keep its
/// existing messages without knowing this service.
/// </remarks>
public static class CheckoutErrorCodes
{
    /// <summary>The quote (order) does not exist.</summary>
    public const string QuoteNotFound = "checkout.quote_not_found";

    /// <summary>The requested combination is not supported.</summary>
    public const string UnsupportedCombination = "checkout.unsupported_combination";

    /// <summary>No provider is able to serve this payment.</summary>
    public const string ProviderUnavailable = "checkout.provider_unavailable";

    /// <summary>The payment does not exist.</summary>
    public const string PaymentNotFound = "checkout.payment_not_found";

    /// <summary>The idempotency key was already used for another order.</summary>
    public const string IdempotencyConflict = "checkout.idempotency_conflict";

    /// <summary>The operation is not available in the current state.</summary>
    public const string OperationUnavailable = "checkout.operation_unavailable";

    /// <summary>The integration is disabled or the API key is missing.</summary>
    public const string Unauthorized = "checkout.unauthorized";
}
