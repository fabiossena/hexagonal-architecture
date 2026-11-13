# ===========================================================================
# OrderFlow API — multi-stage Dockerfile (.NET 8).
# ===========================================================================

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the NuGet layer is cached across source changes.
COPY Directory.Build.props OrderFlow.sln ./
COPY src/OrderFlow.Domain/OrderFlow.Domain.csproj src/OrderFlow.Domain/
COPY src/OrderFlow.Application/OrderFlow.Application.csproj src/OrderFlow.Application/
COPY src/OrderFlow.Adapters.Inbound/OrderFlow.Adapters.Inbound.csproj src/OrderFlow.Adapters.Inbound/
COPY src/OrderFlow.Adapters.Outbound/OrderFlow.Adapters.Outbound.csproj src/OrderFlow.Adapters.Outbound/
COPY src/OrderFlow.Api/OrderFlow.Api.csproj src/OrderFlow.Api/
COPY tests/OrderFlow.Domain.Tests/OrderFlow.Domain.Tests.csproj tests/OrderFlow.Domain.Tests/
COPY tests/OrderFlow.Application.Tests/OrderFlow.Application.Tests.csproj tests/OrderFlow.Application.Tests/
RUN dotnet restore OrderFlow.sln

COPY . .
RUN dotnet publish src/OrderFlow.Api/OrderFlow.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "OrderFlow.Api.dll"]
