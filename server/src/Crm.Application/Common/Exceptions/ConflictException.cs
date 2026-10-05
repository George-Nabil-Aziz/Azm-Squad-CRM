namespace Crm.Application.Common.Exceptions;

/// <summary>Request conflicts with current state (duplicate, concurrency). Mapped to 409 ProblemDetails.</summary>
public sealed class ConflictException(string message) : Exception(message);
