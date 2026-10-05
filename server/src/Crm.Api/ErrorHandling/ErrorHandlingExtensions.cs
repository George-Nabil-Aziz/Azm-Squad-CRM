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
