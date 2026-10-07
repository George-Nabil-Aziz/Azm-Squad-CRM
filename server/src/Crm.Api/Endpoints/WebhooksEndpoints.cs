using Crm.Application.Auth;
using Crm.Application.Integrations;

namespace Crm.Api.Endpoints;

/// <summary>Outgoing webhooks (CRM-59): register, change, enable / disable, delete, delivery log. Needs <c>integrations.manage</c>.</summary>
public static class WebhooksEndpoints
{
    public static IEndpointRouteBuilder MapWebhooksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks").RequireAuthorization(Permissions.IntegrationsManage);

        group.MapGet("", async (IWebhookService webhooks, CancellationToken cancellationToken) =>
            Results.Ok(await webhooks.ListAsync(cancellationToken)));

        // The only response that contains the signing secret.
        group.MapPost("", async (WebhookRequest request, IWebhookService webhooks, CancellationToken cancellationToken) =>
        {
            var created = await webhooks.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/webhooks/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, WebhookRequest request, IWebhookService webhooks, CancellationToken cancellationToken) =>
            Results.Ok(await webhooks.UpdateAsync(id, request, cancellationToken)));

        group.MapDelete("/{id:guid}", async (Guid id, IWebhookService webhooks, CancellationToken cancellationToken) =>
        {
            await webhooks.DeleteAsync(id, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/enable", async (Guid id, IWebhookService webhooks, CancellationToken cancellationToken) =>
            Results.Ok(await webhooks.SetEnabledAsync(id, true, cancellationToken)));

        group.MapPost("/{id:guid}/disable", async (Guid id, IWebhookService webhooks, CancellationToken cancellationToken) =>
            Results.Ok(await webhooks.SetEnabledAsync(id, false, cancellationToken)));

        group.MapGet("/{id:guid}/deliveries", async (Guid id, IWebhookService webhooks, CancellationToken cancellationToken) =>
            Results.Ok(await webhooks.ListDeliveriesAsync(id, cancellationToken)));

        return app;
    }
}
