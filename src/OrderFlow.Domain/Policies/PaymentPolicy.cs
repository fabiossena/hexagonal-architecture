using OrderFlow.Domain.Payments;

namespace OrderFlow.Domain.Policies;

/// <summary>
/// Pure domain rules describing what each provider can do and how a payment
/// status may evolve.
/// </summary>
/// <remarks>
/// Both matrices are static data with no external dependency, so they can be
/// evaluated by the domain, by the application layer (to reject unsupported
/// combinations early) and by the test screen (to render the right form).
/// </remarks>
public static class PaymentPolicy
{
    private static readonly IReadOnlyDictionary<PaymentProvider, IReadOnlyCollection<PaymentMethod>> MethodsByProvider =
        new Dictionary<PaymentProvider, IReadOnlyCollection<PaymentMethod>>
        {
            [PaymentProvider.Stripe] = new[]
            {
                PaymentMethod.CreditCard,
                PaymentMethod.DebitCard,
                PaymentMethod.Pix,
                PaymentMethod.Boleto
            },
            [PaymentProvider.MercadoPago] = new[]
            {
                PaymentMethod.CreditCard,
                PaymentMethod.DebitCard,
                PaymentMethod.Pix,
                PaymentMethod.Boleto,
                PaymentMethod.BankTransfer
            },
            [PaymentProvider.Asaas] = new[]
            {
                PaymentMethod.CreditCard,
                PaymentMethod.DebitCard,
                PaymentMethod.Pix,
                PaymentMethod.Boleto
            },
            [PaymentProvider.Simulated] = new[]
            {
                PaymentMethod.CreditCard,
                PaymentMethod.DebitCard,
                PaymentMethod.Pix,
                PaymentMethod.Boleto,
                PaymentMethod.BankTransfer
            }
        };

    private static readonly IReadOnlyDictionary<PaymentStatus, IReadOnlyCollection<PaymentStatus>> AllowedTransitions =
        new Dictionary<PaymentStatus, IReadOnlyCollection<PaymentStatus>>
        {
            [PaymentStatus.Pending] = new[]
            {
                PaymentStatus.Processing,
                PaymentStatus.Paid,
                PaymentStatus.Failed,
                PaymentStatus.Cancelled
            },
            [PaymentStatus.Processing] = new[]
            {
                PaymentStatus.Paid,
                PaymentStatus.Failed,
                PaymentStatus.Cancelled
            },
            [PaymentStatus.Paid] = new[] { PaymentStatus.Refunded },
            [PaymentStatus.Failed] = Array.Empty<PaymentStatus>(),
            [PaymentStatus.Cancelled] = Array.Empty<PaymentStatus>(),
            [PaymentStatus.Refunded] = Array.Empty<PaymentStatus>()
        };

    /// <summary>Returns the methods supported by a provider.</summary>
    /// <param name="provider">Provider to inspect.</param>
    /// <returns>The supported methods (never empty).</returns>
    public static IReadOnlyCollection<PaymentMethod> MethodsFor(PaymentProvider provider)
    {
        return MethodsByProvider.TryGetValue(provider, out var methods)
            ? methods
            : Array.Empty<PaymentMethod>();
    }

    /// <summary>Checks whether a provider accepts a method.</summary>
    /// <param name="provider">Provider to check.</param>
    /// <param name="method">Method to check.</param>
    /// <returns><c>true</c> when the combination is supported.</returns>
    public static bool Supports(PaymentProvider provider, PaymentMethod method)
    {
        return MethodsFor(provider).Contains(method);
    }

    /// <summary>Checks whether a payment may move from one status to another.</summary>
    /// <param name="from">Current status.</param>
    /// <param name="to">Requested status.</param>
    /// <returns><c>true</c> when the transition is allowed.</returns>
    public static bool CanTransition(PaymentStatus from, PaymentStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    /// <summary>Checks whether a status is final (no further transition is allowed).</summary>
    /// <param name="status">Status to inspect.</param>
    /// <returns><c>true</c> when the status is terminal.</returns>
    public static bool IsTerminal(PaymentStatus status)
    {
        return AllowedTransitions.TryGetValue(status, out var targets) && targets.Count == 0;
    }
}
