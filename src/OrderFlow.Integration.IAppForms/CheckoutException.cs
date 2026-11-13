namespace OrderFlow.Integration.IAppForms;

/// <summary>
/// Error raised by the iAppForms checkout surface.
/// </summary>
/// <remarks>
/// It carries the HTTP status and the structured <c>code</c> that the iAppForms
/// front-end already knows, so the answer is identical to the one the current
/// in-process implementation produces.
/// </remarks>
public sealed class CheckoutException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="statusCode">HTTP status code to answer with.</param>
    /// <param name="code">Structured error code (see <see cref="CheckoutErrorCodes"/>).</param>
    /// <param name="message">Human-readable detail.</param>
    public CheckoutException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>Gets the HTTP status code to answer with.</summary>
    public int StatusCode { get; }

    /// <summary>Gets the structured error code.</summary>
    public string Code { get; }
}
