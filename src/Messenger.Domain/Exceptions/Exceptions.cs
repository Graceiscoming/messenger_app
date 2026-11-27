namespace Messenger.Domain.Exceptions;

/// <summary>Base type for business-rule violations; the API maps each subtype to a ProblemDetails status.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>404</summary>
public sealed class NotFoundException(string message) : DomainException(message);

/// <summary>403</summary>
public sealed class ForbiddenException(string message) : DomainException(message);

/// <summary>409</summary>
public sealed class ConflictException(string message) : DomainException(message);

/// <summary>401 (bad credentials, locked-out account, invalid/expired refresh token)</summary>
public sealed class UnauthorizedException(string message) : DomainException(message);

/// <summary>413</summary>
public sealed class PayloadTooLargeException(string message) : DomainException(message);

/// <summary>400, optionally with per-field errors (field name -> messages).</summary>
public sealed class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
        => Errors = new Dictionary<string, string[]>();

    public ValidationException(string field, string message) : base(message)
        => Errors = new Dictionary<string, string[]> { [field] = new[] { message } };

    public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors) : base(message)
        => Errors = errors;
}
