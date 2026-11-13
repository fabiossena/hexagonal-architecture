namespace OrderFlow.Application.Dtos;

/// <summary>Monetary value exposed by the API.</summary>
/// <param name="Amount">Amount with two decimals.</param>
/// <param name="Currency">ISO-4217 currency code.</param>
public sealed record MoneyDto(decimal Amount, string Currency);
