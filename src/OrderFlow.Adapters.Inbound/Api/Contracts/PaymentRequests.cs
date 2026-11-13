using OrderFlow.Domain.Payments;

namespace OrderFlow.Adapters.Inbound.Api.Contracts;

/// <summary>Body of <c>POST /api/payments</c>.</summary>
/// <param name="OrderId">Order to pay.</param>
/// <param name="Provider">Provider that processes the charge.</param>
/// <param name="Method">Payment instrument.</param>
/// <param name="Amount">Amount to charge (defaults to the order total).</param>
/// <param name="Description">Description shown to the customer.</param>
/// <param name="CustomerEmail">Customer e-mail.</param>
/// <param name="CustomerName">Customer name.</param>
/// <param name="CustomerDocument">Customer tax id (CPF/CNPJ), needed by some providers.</param>
/// <param name="CardToken">Tokenized card produced by the provider's own SDK.</param>
/// <param name="Installments">Number of installments (cards only).</param>
/// <param name="SimulateFailure">Asks the simulator to answer with a failure.</param>
public sealed record CreatePaymentRequest(
    Guid OrderId,
    PaymentProvider Provider,
    PaymentMethod Method,
    decimal? Amount,
    string? Description,
    string? CustomerEmail,
    string? CustomerName,
    string? CustomerDocument,
    string? CardToken,
    int Installments = 1,
    bool SimulateFailure = false);

/// <summary>Body of <c>POST /api/payments/{id}/refund</c>.</summary>
/// <param name="Amount">Amount to refund (defaults to the refundable amount).</param>
public sealed record RefundPaymentRequest(decimal? Amount);

/// <summary>Body of <c>POST /api/payments/{id}/cancel</c>.</summary>
/// <param name="Reason">Why the payment is being cancelled.</param>
public sealed record CancelPaymentRequest(string? Reason);
