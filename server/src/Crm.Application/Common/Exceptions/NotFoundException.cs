namespace Crm.Application.Common.Exceptions;

/// <summary>Requested resource does not exist. Mapped to 404 ProblemDetails.</summary>
public sealed class NotFoundException(string message) : Exception(message);
