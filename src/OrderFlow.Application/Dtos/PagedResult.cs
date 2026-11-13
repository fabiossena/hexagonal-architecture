namespace OrderFlow.Application.Dtos;

/// <summary>Generic page of results.</summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Items in the current page.</param>
/// <param name="Page">One-based page number.</param>
/// <param name="Size">Requested page size.</param>
/// <param name="TotalCount">Total number of items across all pages.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int Size, int TotalCount)
{
    /// <summary>Gets the total number of pages.</summary>
    public int TotalPages => Size <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)Size);
}
