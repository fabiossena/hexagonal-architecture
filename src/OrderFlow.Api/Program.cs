using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderFlow.Adapters.Inbound;
using OrderFlow.Adapters.Outbound;
using OrderFlow.Adapters.Outbound.Persistence;
using OrderFlow.Application;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root: the only place that knows every concrete adapter.
//   Domain / Application  -> framework-free
//   Adapters.Inbound      -> HTTP controllers
//   Adapters.Outbound     -> EF Core + PostgreSQL, system clock, payment providers
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInboundAdapters();
builder.Services.AddOutboundAdapters(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrderFlow Payment API",
        Version = "v1",
        Description = "Order payment service built with Hexagonal Architecture (Ports & Adapters). " +
                      "Supports Stripe, Mercado Pago, Asaas and an offline simulator."
    });
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<OrderFlowDbContext>("database");

var app = builder.Build();

// Domain exceptions are translated to problem details by the inbound adapter.
app.UseExceptionHandler();
app.UseStatusCodePages();

// The test screen lives in wwwroot and is served as the home page.
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderFlow Payment API v1"));
}

app.MapControllers();
app.MapHealthChecks("/health");

await InitializeDatabaseAsync(app);

app.Run();

// Creates the schema on first run (or applies migrations when they exist).
// A failing database must not prevent the host from starting: /health will
// report the dependency as unhealthy instead.
static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();

    try
    {
        if (dbContext.Database.GetMigrations().Any())
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        logger.LogInformation("Order/payment database is ready.");
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Could not initialize the order database. /health will report it as unhealthy.");
    }
}

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program
{
}
