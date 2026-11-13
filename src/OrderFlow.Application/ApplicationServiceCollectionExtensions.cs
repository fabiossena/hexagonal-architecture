using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Orders.CancelOrder;
using OrderFlow.Application.Orders.ConfirmOrder;
using OrderFlow.Application.Orders.GetOrder;
using OrderFlow.Application.Orders.ListOrders;
using OrderFlow.Application.Orders.PlaceOrder;
using OrderFlow.Application.Payments.ApplyProviderCallback;
using OrderFlow.Application.Payments.CancelPayment;
using OrderFlow.Application.Payments.CreatePayment;
using OrderFlow.Application.Payments.GetPayment;
using OrderFlow.Application.Payments.ListPayments;
using OrderFlow.Application.Payments.ListProviders;
using OrderFlow.Application.Payments.RefreshPaymentStatus;
using OrderFlow.Application.Payments.RefundPayment;

namespace OrderFlow.Application;

/// <summary>
/// Registers the application use cases in the dependency-injection container.
/// </summary>
/// <remarks>
/// The composition root (the API host) calls this extension; the application
/// layer still has no knowledge of ASP.NET or EF Core (only the DI abstractions).
/// </remarks>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Adds every use case as a scoped service.</summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<PlaceOrderUseCase>();
        services.AddScoped<ConfirmOrderUseCase>();
        services.AddScoped<CancelOrderUseCase>();
        services.AddScoped<GetOrderUseCase>();
        services.AddScoped<ListOrdersUseCase>();

        services.AddScoped<CreatePaymentUseCase>();
        services.AddScoped<GetPaymentUseCase>();
        services.AddScoped<ListPaymentsUseCase>();
        services.AddScoped<RefundPaymentUseCase>();
        services.AddScoped<CancelPaymentUseCase>();
        services.AddScoped<RefreshPaymentStatusUseCase>();
        services.AddScoped<ListPaymentProvidersUseCase>();
        services.AddScoped<ApplyProviderCallbackUseCase>();

        return services;
    }
}
