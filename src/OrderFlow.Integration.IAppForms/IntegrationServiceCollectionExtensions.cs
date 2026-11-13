using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Registers the iAppForms compatibility surface into the dependency-injection
/// container.
/// </summary>
public static class IntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the iAppForms checkout surface: its controllers, its problem-details
    /// handler and the options it reads.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddIAppFormsIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<IAppFormsOptions>()
            .Bind(configuration.GetSection(IAppFormsOptions.SectionName));

        services.AddScoped<IAppFormsPaymentSurface>();

        services
            .AddControllers()
            .AddApplicationPart(typeof(IntegrationServiceCollectionExtensions).Assembly);

        services.AddExceptionHandler<CheckoutExceptionHandler>();

        return services;
    }
}
