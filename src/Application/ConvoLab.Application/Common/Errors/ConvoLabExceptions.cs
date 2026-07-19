namespace ConvoLab.Application.Common.Errors;

public enum ErrorCategory
{
    Validation,
    NotFound,
    Conflict,
    DomainRule,
    ExternalDependency,
    Unavailable
}

public abstract class ConvoLabException : Exception
{
    protected ConvoLabException(
        string code,
        string message,
        ErrorCategory category,
        IReadOnlyDictionary<string, string[]>? validationErrors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Category = category;
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
    }

    public string Code { get; }
    public ErrorCategory Category { get; }
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }
}

public sealed class RequestValidationException : ConvoLabException
{
    public RequestValidationException(string code, string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(code, message, ErrorCategory.Validation, errors) { }
}

public sealed class ResourceNotFoundException : ConvoLabException
{
    public ResourceNotFoundException(string code, string message)
        : base(code, message, ErrorCategory.NotFound) { }
}

public sealed class DomainRuleViolationException : ConvoLabException
{
    public DomainRuleViolationException(string code, string message)
        : base(code, message, ErrorCategory.DomainRule) { }
}

public sealed class ResourceConflictException : ConvoLabException
{
    public ResourceConflictException(string code, string message)
        : base(code, message, ErrorCategory.Conflict) { }
}

public sealed class ConcurrencyConflictException : ConvoLabException
{
    public ConcurrencyConflictException(string resource, Guid id)
        : this($"The {resource} '{id}' was changed by another operation. Refresh the resource and retry.") { }

    public ConcurrencyConflictException(string message)
        : base("concurrency.conflict", message, ErrorCategory.Conflict) { }
}

public sealed class ExternalDependencyException : ConvoLabException
{
    public ExternalDependencyException(string code, string message, Exception? innerException = null)
        : base(code, message, ErrorCategory.ExternalDependency, innerException: innerException) { }
}

public sealed class CapabilityUnavailableException : ConvoLabException
{
    public CapabilityUnavailableException(string code, string message)
        : base(code, message, ErrorCategory.Unavailable) { }
}
