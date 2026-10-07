using Crm.Application.Auth;
using Crm.Application.Integrations;

namespace Crm.Api.Endpoints;

/// <summary>Admin side of the public API (CRM-58): create, list and revoke API keys. Needs <c>integrations.manage</c>.</summary>
public static class ApiKeysEndpoints
{
    public static IEndpointRouteBuilder MapApiKeysEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/api-keys").RequireAuthorization(Permissions.IntegrationsManage).ExcludeFromDescription();

        group.MapGet("", async (IApiKeyService keys, CancellationToken cancellationToken) =>
            Results.Ok(await keys.ListAsync(cancellationToken)));

        // The only response that contains the key itself.
        group.MapPost("", async (CreateApiKeyRequest request, IApiKeyService keys, CancellationToken cancellationToken) =>
        {
            var created = await keys.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/api-keys/{created.Id}", created);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IApiKeyService keys, CancellationToken cancellationToken) =>
        {
            await keys.RevokeAsync(id, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
