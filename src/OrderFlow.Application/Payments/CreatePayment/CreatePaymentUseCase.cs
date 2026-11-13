using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Payments;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Payments.CreatePayment;

/// <summary>
/// Use case: charge an order through a payment provider.
/// </summary>
/// <remarks>
/// <para>
/// Orchestration only — every invariant is enforced by the aggregate. The use
/// case depends on outbound ports (<see cref="IPaymentRepository"/>,
/// <see cref="IPaymentProviderResolver"/>, <see cref="IUnitOfWork"/>,
/// <see cref="IClock"/>), never on a concrete provider client.
/// </para>
/// <para>
/// When the provider call blows up, the attempt is still recorded as a failed
/// payment (so the audit trail is complete) and the original error is rethrown.
/// </para>
/// </remarks>
public sealed class CreatePaymentUseCase
{
    /// <summary>Maximum number of installments accepted for card payments.</summary>
    public const int MaxInstallments = 12;

    private readonly IPaymentRepository _payments;
    private readonly IOrderRepository _orders;
    private readonly IPaymentProviderResolver _providers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the use case.</summary>
    /// <param name="payments">Payment repository port.</param>
    /// <param name="orders">Order repository port.</param>
    /// <param name="providers">Provider resolver port.</param>
    /// <param name="unitOfWork">Transaction port.</param>
    /// <param name="clock">Clock port.</param>
    public CreatePaymentUseCase(
        IPaymentRepository payments,
        IOrderRepository orders,
        IPaymentProviderResolver providers,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _payments = payments;
        _orders = orders;
        _providers = providers;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Charges the customer and records the payment.</summary>
    /// <param name="command">Payment data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted payment as a read model.</returns>
    /// <exception cref="OrderNotFoundException">The order does not exist.</exception>
    /// <exception cref="ConflictException">The order is not confirmed.</exception>
    /// <exception cref="ValidationException">The command is invalid or the provider cannot be used.</exception>
    /// <exception cref="PaymentProviderException">The provider call failed.</exception>
    public async Task<PaymentDto> ExecuteAsync(
        CreatePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await _orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new OrderNotFoundException(command.OrderId);

        // Replaying an idempotency key must never charge the customer twice.
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existing = await _payments.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);

            if (existing is not null)
            {
                if (existing.OrderId != order.Id)
                {
                    throw new ConflictException(
                        "The idempotency key was already used for another order.");
                }

                return PaymentMapper.ToDto(existing, reused: true);
            }
        }

        if (order.Status != OrderStatus.Confirmed)
        {
            throw new ConflictException(
                $"Only confirmed orders can be paid (current status: {order.Status}).");
        }

        var provider = _providers.Resolve(command.Provider);

        if (!provider.IsConfigured)
        {
            throw new ValidationException(
                $"Provider '{provider.DisplayName}' is not configured. Set its credentials in the configuration.");
        }

        if (!provider.SupportedMethods.Contains(command.Method))
        {
            throw new ValidationException(
                $"Provider '{provider.DisplayName}' does not support the method '{command.Method}'.");
        }

        var amount = ResolveAmount(command, order);
        var installments = ResolveInstallments(command);
        var description = string.IsNullOrWhiteSpace(command.Description)
            ? $"Order {order.Id:N}"
            : command.Description.Trim();

        var nowUtc = _clock.UtcNow;
        var payment = Payment.Create(
            order.Id,
            command.Provider,
            command.Method,
            amount,
            description,
            command.CustomerEmail,
            nowUtc,
            command.IdempotencyKey);

        var request = new ProviderChargeRequest(
            order.Id,
            amount,
            command.Method,
            description,
            command.CustomerEmail,
            command.CustomerName,
            command.CustomerDocument,
            command.CardToken,
            installments,
            command.IdempotencyKey ?? payment.Id.ToString("N"),
            command.CallbackUrl,
            BuildMetadata(command));

        ProviderChargeResult result;
        try
        {
            result = await provider.ChargeAsync(request, cancellationToken);
        }
        catch (PaymentProviderException exception)
        {
            payment.MarkFailed(null, null, exception.Message, nowUtc);
            payment.AttachProviderPayload(exception.ProviderPayload);

            await _payments.AddAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw;
        }

        ApplyResult(payment, result, nowUtc);

        await _payments.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PaymentMapper.ToDto(payment);
    }

    private static Money ResolveAmount(CreatePaymentCommand command, Order order)
    {
        var requested = command.Amount ?? order.Total.Amount;

        if (requested <= 0m)
        {
            throw new ValidationException("The payment amount must be greater than zero.");
        }

        if (requested > order.Total.Amount)
        {
            throw new ValidationException(
                $"The payment amount ({requested} {order.Total.Currency}) cannot exceed the order total " +
                $"({order.Total.Amount} {order.Total.Currency}).");
        }

        return new Money(requested, order.Total.Currency);
    }

    private static int ResolveInstallments(CreatePaymentCommand command)
    {
        if (command.Method is not (PaymentMethod.CreditCard or PaymentMethod.DebitCard))
        {
            return 1;
        }

        if (command.Installments is < 1 or > MaxInstallments)
        {
            throw new ValidationException($"The number of installments must be between 1 and {MaxInstallments}.");
        }

        return command.Installments;
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(CreatePaymentCommand command)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["order_id"] = command.OrderId.ToString("N")
        };

        if (command.SimulateFailure)
        {
            metadata["simulate_failure"] = "true";
        }

        return metadata;
    }

    private static void ApplyResult(Payment payment, ProviderChargeResult result, DateTime nowUtc)
    {
        switch (result.Status)
        {
            case PaymentStatus.Paid:
                payment.MarkPaid(result.ProviderPaymentId, result.ProviderStatus ?? "paid", nowUtc);
                break;
            case PaymentStatus.Processing:
                payment.MarkProcessing(result.ProviderPaymentId, result.ProviderStatus ?? "processing", nowUtc);
                break;
            case PaymentStatus.Failed:
                payment.MarkFailed(
                    result.ProviderPaymentId,
                    result.ProviderStatus,
                    result.FailureReason ?? "The provider rejected the charge.",
                    nowUtc);
                break;
            case PaymentStatus.Pending:
            case PaymentStatus.Cancelled:
            case PaymentStatus.Refunded:
                break;
            default:
                break;
        }

        payment.AttachCheckoutDetails(result.CheckoutUrl, result.PixCopyPasteCode, result.PixQrCodeBase64);
        payment.AttachProviderPayload(result.RawResponse);
    }
}
