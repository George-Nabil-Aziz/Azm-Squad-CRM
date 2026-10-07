using Crm.Application.Ai;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.Common.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.ErrorHandling;

/// <summary>
/// Turns every exception that escapes an endpoint into an RFC 7807 ProblemDetails response and logs it
/// with the request's correlation id. Never exposes exception details outside Development.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);
        var problem = ToProblemDetails(exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception,
                "Unhandled exception for {Method} {Path}. CorrelationId: {CorrelationId}",
                httpContext.Request.Method, httpContext.Request.Path, correlationId);

            if (environment.IsDevelopment())
            {
                problem.Detail = exception.ToString();
            }
        }
        else
        {
            logger.LogWarning(
                "Request {Method} {Path} failed with {StatusCode} ({ExceptionType}). CorrelationId: {CorrelationId}",
                httpContext.Request.Method, httpContext.Request.Path, problem.Status, exception.GetType().Name, correlationId);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        if (exception is RateLimitExceededException limited)
        {
            httpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(limited.RetryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails ToProblemDetails(Exception exception) => exception switch
    {
        ValidationException validation => new HttpValidationProblemDetails(
            validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = ErrorText.ValidationFailed,
        },
        BadHttpRequestException badRequest => new ProblemDetails
        {
            Status = badRequest.StatusCode,
            Title = ErrorText.MalformedRequest,
        },
        UnauthorizedException unauthorized => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = ErrorText.AuthenticationFailed,
            Detail = unauthorized.Message,
        },
        NotFoundException notFound => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = ErrorText.NotFound,
            Detail = notFound.Message,
        },
        ConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = ErrorText.Conflict,
            Detail = conflict.Message,
        },
        RateLimitExceededException => new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = ErrorText.TooManyRequests,
        },
        AiNotConfiguredException notConfigured => new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = AiText.NotConfiguredTitle,
            Detail = notConfigured.Message,
        },
        AiFailedException => new ProblemDetails
        {
            Status = StatusCodes.Status502BadGateway,
            Title = AiText.FailedTitle,
            Detail = AiText.Failed,
        },
        ForbiddenException forbidden => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = ErrorText.Forbidden,
            Detail = forbidden.Message,
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = ErrorText.Unexpected,
        },
    };
}
