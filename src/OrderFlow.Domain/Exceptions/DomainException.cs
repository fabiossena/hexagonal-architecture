namespace OrderFlow.Domain.Exceptions;

/// <summary>
/// Base type for every error that represents a broken business rule.
/// </summary>
/// <remarks>
/// The domain never references HTTP, so the mapping to status codes is done by
/// the inbound adapter (see <c>DomainExceptionHandler</c>).
/// </remarks>
public abstract class DomainException : Exception
{
    /// <summary>Creates the exception with a human-readable message.</summary>
    /// <param name="message">Description of the violated rule.</param>
    protected DomainException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Input or state is invalid (maps to HTTP 400).
/// </summary>
public sealed class ValidationException : DomainException
{
    /// <summary>Creates the exception with a human-readable message.</summary>
    /// <param name="message">Description of the invalid input.</param>
    public ValidationException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// The request conflicts with the current state of the aggregate (maps to HTTP 409).
/// </summary>
public sealed class ConflictException : DomainException
{
    /// <summary>Creates the exception with a human-readable message.</summary>
    /// <param name="message">Description of the state conflict.</param>
    public ConflictException(string message)
        : base(message)
    {
    }
}
