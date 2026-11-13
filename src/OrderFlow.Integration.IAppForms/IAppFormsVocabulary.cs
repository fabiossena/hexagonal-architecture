using OrderFlow.Domain.Payments;

namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Translates between the OrderFlow vocabulary and the iAppForms one.
/// </summary>
/// <remarks>
/// <para>
/// iAppForms speaks two different status languages:
/// </para>
/// <list type="bullet">
///   <item><description>
///     the <b>commercial</b> status (<see cref="PlanPaymentStatus"/>): the
///     review outcome of the payment — <c>InReview</c>, <c>Approved</c>,
///     <c>Rejected</c>, <c>Error</c>, <c>TimedOut</c>, <c>Cancelled</c>;
///   </description></item>
///   <item><description>
///     the <b>provider</b> status (<see cref="ProviderStatus"/>): the settlement
///     reported by the gateway — <c>Pending</c>, <c>Paid</c>, <c>Failed</c>,
///     <c>Cancelled</c>, <c>Refunded</c>, <c>Chargeback</c>.
///   </description></item>
/// </list>
/// <para>
/// This class is the anti-corruption layer: the domain keeps its own single
/// status and never learns the iAppForms tokens.
/// </para>
/// </remarks>
public static class IAppFormsVocabulary
{
    /// <summary>Commercial status of a payment, as iAppForms names it.</summary>
    public static class PlanPaymentStatus
    {
        /// <summary>The payment is waiting for a decision (the 72h window).</summary>
        public const string InReview = "InReview";

        /// <summary>The payment was approved.</summary>
        public const string Approved = "Approved";

        /// <summary>The provider refused the payment.</summary>
        public const string Rejected = "Rejected";

        /// <summary>The payment could not be processed.</summary>
        public const string Error = "Error";

        /// <summary>The confirmation window expired.</summary>
        public const string TimedOut = "TimedOut";

        /// <summary>The payment was cancelled.</summary>
        public const string Cancelled = "Cancelled";
    }

    /// <summary>Settlement status reported by the provider, as iAppForms names it.</summary>
    public static class ProviderStatus
    {
        /// <summary>The provider has not settled the payment yet.</summary>
        public const string Pending = "Pending";

        /// <summary>The money was captured.</summary>
        public const string Paid = "Paid";

        /// <summary>The provider refused the charge.</summary>
        public const string Failed = "Failed";

        /// <summary>The charge was cancelled.</summary>
        public const string Cancelled = "Cancelled";

        /// <summary>The money was returned.</summary>
        public const string Refunded = "Refunded";

        /// <summary>The money was taken back by the issuer.</summary>
        public const string Chargeback = "Chargeback";
    }

    /// <summary>Translates the OrderFlow status into the iAppForms provider status.</summary>
    /// <param name="status">OrderFlow status.</param>
    /// <returns>The iAppForms provider token.</returns>
    public static string ToProviderStatus(PaymentStatus status)
    {
        return status switch
        {
            PaymentStatus.Pending or PaymentStatus.Processing => ProviderStatus.Pending,
            PaymentStatus.Paid => ProviderStatus.Paid,
            PaymentStatus.Failed => ProviderStatus.Failed,
            PaymentStatus.Cancelled => ProviderStatus.Cancelled,
            PaymentStatus.Refunded => ProviderStatus.Refunded,
            _ => ProviderStatus.Pending
        };
    }

    /// <summary>Translates the OrderFlow status into the iAppForms commercial status.</summary>
    /// <param name="status">OrderFlow status.</param>
    /// <param name="hasProviderReference">
    /// <c>true</c> when the provider answered with a reference — the charge was
    /// declined; <c>false</c> when the call itself failed — a processing error.
    /// </param>
    /// <returns>The iAppForms commercial token.</returns>
    public static string ToPlanPaymentStatus(PaymentStatus status, bool hasProviderReference)
    {
        return status switch
        {
            PaymentStatus.Pending or PaymentStatus.Processing => PlanPaymentStatus.InReview,
            PaymentStatus.Paid => PlanPaymentStatus.Approved,

            // The provider answered and refused the charge.
            PaymentStatus.Failed when hasProviderReference => PlanPaymentStatus.Rejected,

            // The provider never answered: the payment could not be processed.
            PaymentStatus.Failed => PlanPaymentStatus.Error,

            PaymentStatus.Cancelled => PlanPaymentStatus.Cancelled,

            // It *was* approved; the provider status carries the refund.
            PaymentStatus.Refunded => PlanPaymentStatus.Approved,
            _ => PlanPaymentStatus.InReview
        };
    }

    /// <summary>Translates an iAppForms method code into the OrderFlow instrument.</summary>
    /// <param name="methodCode">iAppForms method code (<c>Pix</c>, <c>CreditCard</c>, …).</param>
    /// <param name="method">The matching OrderFlow instrument.</param>
    /// <returns><c>true</c> when the code is known.</returns>
    public static bool TryParseMethod(string? methodCode, out PaymentMethod method)
    {
        method = PaymentMethod.Pix;

        if (string.IsNullOrWhiteSpace(methodCode))
        {
            return false;
        }

        return Enum.TryParse(methodCode.Trim(), ignoreCase: true, out method) &&
               Enum.IsDefined(method);
    }

    /// <summary>Translates an OrderFlow instrument into the iAppForms method code.</summary>
    /// <param name="method">OrderFlow instrument.</param>
    /// <returns>The iAppForms method code (same token).</returns>
    public static string ToMethodCode(PaymentMethod method) => method.ToString();

    /// <summary>Translates an OrderFlow provider into the iAppForms adapter name.</summary>
    /// <param name="provider">OrderFlow provider.</param>
    /// <returns>The adapter name (<c>Stripe</c>, <c>MercadoPago</c>, <c>Asaas</c>, <c>Simulated</c>).</returns>
    public static string ToProviderName(PaymentProvider provider) => provider.ToString();

    /// <summary>Translates an iAppForms adapter name into the OrderFlow provider.</summary>
    /// <param name="providerName">Adapter name.</param>
    /// <param name="provider">The matching OrderFlow provider.</param>
    /// <returns><c>true</c> when the name is known.</returns>
    public static bool TryParseProvider(string? providerName, out PaymentProvider provider)
    {
        provider = PaymentProvider.Simulated;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            return false;
        }

        return Enum.TryParse(providerName.Trim(), ignoreCase: true, out provider) &&
               Enum.IsDefined(provider);
    }
}
