using OrderFlow.Domain.Payments;

namespace OrderFlow.Application.Payments.CreatePayment;

/// <summary>Input of <see cref="CreatePaymentUseCase"/>.</summary>
/// <param name="OrderId">Order to pay.</param>
/// <param name="Provider">Provider that processes the charge.</param>
/// <param name="Method">Payment instrument.</param>
/// <param name="Amount">Amount to charge (defaults to the order total when omitted).</param>
/// <param name="Description">Description shown to the customer (defaults to the order reference).</param>
/// <param name="CustomerEmail">Customer e-mail.</param>
/// <param name="CustomerName">Customer name.</param>
/// <param name="CustomerDocument">Customer tax id (CPF/CNPJ), needed by some providers.</param>
/// <param name="CardToken">Tokenized card produced by the provider's own SDK.</param>
/// <param name="Installments">Number of installments (cards only).</param>
/// <param name="SimulateFailure">Asks the simulator to answer with a failure (test screen helper).</param>
/// <param name="CallbackUrl">URL the provider calls back with status updates.</param>
/// <param name="IdempotencyKey">Optional client key that prevents duplicated charges.</param>
public sealed record CreatePaymentCommand(
    Guid OrderId,
    PaymentProvider Provider,
    PaymentMethod Method,
    decimal? Amount = null,
    string? Description = null,
    string? CustomerEmail = null,
    string? CustomerName = null,
    string? CustomerDocument = null,
    string? CardToken = null,
    int Installments = 1,
    bool SimulateFailure = false,
    string? CallbackUrl = null,
    string? IdempotencyKey = null);
