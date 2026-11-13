using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Adapters.Inbound.Api.ErrorHandling;

/// <summary>
/// Translates domain/application exceptions into RFC 9457 problem details.
/// </summary>
/// <remarks>
/// This is the inbound adapter's job: the domain expresses *what* went wrong,
/// the HTTP adapter decides *which status code* represents it.
/// <list type="bullet">
///   <item><description><see cref="ValidationException"/> → 400</description></item>
///   <item><description><see cref="ConflictException"/> → 409</description></item>
///   <item><description><see cref="OrderNotFoundException"/> → 404</description></item>
///   <item><description><see cref="PaymentNotFoundException"/> → 404</description></item>
///   <item><description><see cref="PaymentProviderException"/> → 502</description></item>
/// </list>
/// </remarks>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<DomainExceptionHandler> _logger;

    /// <summary>Creates the handler.</summary>
    /// <param name="problemDetailsService">Problem-details writer.</param>
    /// <param name="logger">Logger.</param>
    public DomainExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<DomainExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation error"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            OrderNotFoundException => (StatusCodes.Status404NotFound, "Order not found"),
            PaymentNotFoundException => (StatusCodes.Status404NotFound, "Payment not found"),
            PaymentProviderException => (StatusCodes.Status502BadGateway, "Payment provider error"),
            _ => (0, string.Empty)
        };

        if (statusCode == 0)
        {
            return false;
        }

        _logger.LogInformation(
            "Request {Method} {Path} failed with {Status}: {Message}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode,
            exception.Message);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        if (exception is PaymentProviderException providerException && providerException.ProviderPayload is { } payload)
        {
            problemDetails.Extensions["providerPayload"] = payload;
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
