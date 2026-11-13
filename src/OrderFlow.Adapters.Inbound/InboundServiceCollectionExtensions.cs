using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Adapters.Inbound.Api;
using OrderFlow.Adapters.Inbound.Api.ErrorHandling;

namespace OrderFlow.Adapters.Inbound;

/// <summary>
/// Wires the inbound adapters (HTTP controllers + problem-details mapping) into DI.
/// </summary>
public static class InboundServiceCollectionExtensions
{
    /// <summary>
    /// Registers MVC (scanning this assembly for controllers), problem-details
    /// support and the domain-exception handler.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddInboundAdapters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                // Enums travel as readable strings ("Pix", "Stripe", "Paid").
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .AddApplicationPart(typeof(OrdersController).Assembly);

        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();

        return services;
    }
}
