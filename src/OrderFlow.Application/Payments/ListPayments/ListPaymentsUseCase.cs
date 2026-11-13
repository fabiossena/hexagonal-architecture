using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Dtos;
using OrderFlow.Domain.Payments;

namespace OrderFlow.Application.Payments.ListPayments;

/// <summary>Input of <see cref="ListPaymentsUseCase"/>.</summary>
/// <param name="Page">One-based page number (clamped to ≥ 1).</param>
/// <param name="Size">Page size (clamped to 1..100).</param>
/// <param name="OrderId">Optional order filter.</param>
/// <param name="Provider">Optional provider filter.</param>
/// <param name="Status">Optional status filter.</param>
public sealed record ListPaymentsQuery(
    int Page = 1,
    int Size = 20,
    Guid? OrderId = null,
    PaymentProvider? Provider = null,
    PaymentStatus? Status = null);

/// <summary>
/// Use case: list payments with pagination and optional filters.
/// </summary>
public sealed class ListPaymentsUseCase
{
    /// <summary>Maximum page size accepted.</summary>
    public const int MaxPageSize = 100;

    private readonly IPaymentRepository _payments;

    /// <summary>Creates the use case.</summary>
    /// <param name="payments">Payment repository port.</param>
    public ListPaymentsUseCase(IPaymentRepository payments)
    {
        _payments = payments;
    }

    /// <summary>Returns a page of payments.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The paged result.</returns>
    public async Task<PagedResult<PaymentDto>> ExecuteAsync(
        ListPaymentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page < 1 ? 1 : query.Page;
        var size = Math.Clamp(query.Size, 1, MaxPageSize);

        var totalCount = await _payments.CountAsync(query.OrderId, query.Provider, query.Status, cancellationToken);
        var payments = await _payments.ListAsync(
            query.OrderId,
            query.Provider,
            query.Status,
            (page - 1) * size,
            size,
            cancellationToken);

        return new PagedResult<PaymentDto>(
            payments.Select(PaymentMapper.ToDto).ToList(),
            page,
            size,
            totalCount);
    }
}
