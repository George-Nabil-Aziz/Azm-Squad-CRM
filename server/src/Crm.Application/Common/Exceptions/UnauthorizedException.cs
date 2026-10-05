namespace Crm.Application.Common.Exceptions;

/// <summary>Caller could not be authenticated (e.g. wrong email or password). Mapped to 401 ProblemDetails.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);
