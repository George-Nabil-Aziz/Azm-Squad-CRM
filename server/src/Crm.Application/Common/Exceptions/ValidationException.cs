namespace Crm.Application.Common.Exceptions;

/// <summary>Request failed validation. Mapped to 400 ProblemDetails with field errors by the API.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// <summary>Field name (camelCase, as in the JSON request) → error messages.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
