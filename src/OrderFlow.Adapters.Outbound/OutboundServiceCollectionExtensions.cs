using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrderFlow.Adapters.Outbound.Payments;
using OrderFlow.Adapters.Outbound.Payments.Asaas;
using OrderFlow.Adapters.Outbound.Payments.MercadoPago;
using OrderFlow.Adapters.Outbound.Payments.Simulated;
using OrderFlow.Adapters.Outbound.Payments.Stripe;
using OrderFlow.Adapters.Outbound.Persistence;
using OrderFlow.Adapters.Outbound.Time;
using OrderFlow.Application.Abstractions;

namespace OrderFlow.Adapters.Outbound;

/// <summary>
/// Wires the outbound adapters (EF Core + PostgreSQL, system clock and the
/// payment providers) into DI.
/// </summary>
public static class OutboundServiceCollectionExtensions
{
    /// <summary>Name of the connection string used by this adapter.</summary>
    public const string ConnectionStringName = "Orders";

    /// <summary>
    /// Registers the PostgreSQL context, the port implementations and every
    /// payment provider adapter.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The connection string is missing.</exception>
    public static IServiceCollection AddOutboundAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<OrderFlowDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();
        services.TryAddSingleton(TimeProvider.System);

        AddPaymentProviders(services, configuration);

        services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();

        return services;
    }

    private static void AddPaymentProviders(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName));
        services.AddOptions<MercadoPagoOptions>()
            .Bind(configuration.GetSection(MercadoPagoOptions.SectionName));
        services.AddOptions<AsaasOptions>()
            .Bind(configuration.GetSection(AsaasOptions.SectionName));
        services.AddOptions<SimulatedOptions>()
            .Bind(configuration.GetSection(SimulatedOptions.SectionName));

        services.AddHttpClient<StripePaymentProvider>((provider, client) =>
            ConfigureClient(client, provider.GetRequiredService<IOptions<StripeOptions>>().Value.BaseUrl));
        services.AddTransient<IPaymentProvider>(provider =>
            provider.GetRequiredService<StripePaymentProvider>());

        services.AddHttpClient<MercadoPagoPaymentProvider>((provider, client) =>
            ConfigureClient(client, provider.GetRequiredService<IOptions<MercadoPagoOptions>>().Value.BaseUrl));
        services.AddTransient<IPaymentProvider>(provider =>
            provider.GetRequiredService<MercadoPagoPaymentProvider>());

        services.AddHttpClient<AsaasPaymentProvider>((provider, client) =>
            ConfigureClient(client, provider.GetRequiredService<IOptions<AsaasOptions>>().Value.BaseUrl));
        services.AddTransient<IPaymentProvider>(provider =>
            provider.GetRequiredService<AsaasPaymentProvider>());

        // The simulator keeps its state in memory, so it is a singleton.
        services.AddSingleton<SimulatedPaymentProvider>();
        services.AddSingleton<IPaymentProvider>(provider =>
            provider.GetRequiredService<SimulatedPaymentProvider>());
    }

    private static void ConfigureClient(HttpClient client, string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        }

        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OrderFlow/1.0");
    }
}
