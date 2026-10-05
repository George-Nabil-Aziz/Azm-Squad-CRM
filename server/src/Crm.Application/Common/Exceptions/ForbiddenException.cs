namespace Crm.Application.Common.Exceptions;

/// <summary>Caller is authenticated but not allowed to do this. Mapped to 403 ProblemDetails.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
