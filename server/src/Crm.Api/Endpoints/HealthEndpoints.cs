namespace Crm.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok")))
           .AllowAnonymous()
           .WithName("GetHealth");
        return app;
    }
}

public sealed record HealthResponse(string Status);
