using Crm.Application.Common.Localization;

namespace Crm.Api.ErrorHandling;

public static class ErrorHandlingExtensions
{
    /// <summary>ProblemDetails for every error response, with <c>correlationId</c> and <c>instance</c> filled in.</summary>
    public static IServiceCollection AddCrmErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["correlationId"] =
                    CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);

                // Empty error responses turned into ProblemDetails by UseStatusCodePages (JWT challenge 401,
                // unknown route 404, malformed body 400, …) have no exception: give them a title in the
                // request language. GlobalExceptionHandler sets its own titles.
                if (context.Exception is null)
                {
                    context.ProblemDetails.Title = context.ProblemDetails.Status switch
                    {
                        StatusCodes.Status400BadRequest => ErrorText.MalformedRequest,
                        StatusCodes.Status401Unauthorized => ErrorText.AuthenticationRequired,
                        StatusCodes.Status403Forbidden => ErrorText.Forbidden,
                        StatusCodes.Status404NotFound => ErrorText.NotFound,
                        _ => context.ProblemDetails.Title,
                    };
                }
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>Order matters: correlation id first (outermost), then the exception handler, then status-code pages.</summary>
    public static WebApplication UseCrmErrorHandling(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
