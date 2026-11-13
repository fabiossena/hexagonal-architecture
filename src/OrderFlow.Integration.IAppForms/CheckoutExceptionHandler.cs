using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Translates a <see cref="CheckoutException"/> into the RFC 9457 problem the
/// iAppForms front-end already understands.
/// </summary>
/// <remarks>
/// The answer carries the structured <c>code</c> extension, exactly like the
/// current in-process checkout implementation.
/// </remarks>
public sealed class CheckoutExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<CheckoutExceptionHandler> _logger;

    /// <summary>Creates the handler.</summary>
    /// <param name="problemDetailsService">Problem-details writer.</param>
    /// <param name="logger">Logger.</param>
    public CheckoutExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<CheckoutExceptionHandler> logger)
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

        if (exception is not CheckoutException checkoutException)
        {
            return false;
        }

        _logger.LogInformation(
            "Checkout request {Method} {Path} failed with {Status}: {Message}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            checkoutException.StatusCode,
            checkoutException.Message);

        httpContext.Response.StatusCode = checkoutException.StatusCode;

        var problem = new ProblemDetails
        {
            Status = checkoutException.StatusCode,
            Title = checkoutException.StatusCode switch
            {
                StatusCodes.Status400BadRequest => "Checkout Error",
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status404NotFound => "Not Found",
                StatusCodes.Status502BadGateway => "Checkout Error",
                _ => "Checkout Error"
            },
            Detail = checkoutException.Message,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["code"] = checkoutException.Code;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}
