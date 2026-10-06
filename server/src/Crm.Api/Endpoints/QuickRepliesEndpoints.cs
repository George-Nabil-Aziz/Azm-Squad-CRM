using Crm.Application.Auth;
using Crm.Application.QuickReplies;

namespace Crm.Api.Endpoints;

public static class QuickRepliesEndpoints
{
    public static IEndpointRouteBuilder MapQuickRepliesEndpoints(this IEndpointRouteBuilder app)
    {
        // Agents use quick replies (tickets.manage); changing shared ones also needs quick-replies.manage-shared (the service answers 403).
        var group = app.MapGroup("/api/quick-replies").RequireAuthorization(Permissions.TicketsManage);

        group.MapGet("", async ([AsParameters] ListQuickRepliesQuery query, IQuickReplyService replies, CancellationToken cancellationToken) =>
                Results.Ok(await replies.ListAsync(query, cancellationToken)))
            .WithName("ListQuickReplies");

        group.MapPost("", async (QuickReplyRequest request, IQuickReplyService replies, CancellationToken cancellationToken) =>
            {
                var reply = await replies.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/quick-replies/{reply.Id}", reply);
            })
            .WithName("CreateQuickReply");

        group.MapPut("/{id:guid}", async (Guid id, QuickReplyRequest request, IQuickReplyService replies, CancellationToken cancellationToken) =>
                Results.Ok(await replies.UpdateAsync(id, request, cancellationToken)))
            .WithName("UpdateQuickReply");

        group.MapDelete("/{id:guid}", async (Guid id, IQuickReplyService replies, CancellationToken cancellationToken) =>
            {
                await replies.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteQuickReply");

        group.MapPost("/{id:guid}/render", async (Guid id, RenderQuickReplyRequest request, IQuickReplyService replies,
                    CancellationToken cancellationToken) =>
                Results.Ok(await replies.RenderAsync(id, request, cancellationToken)))
            .WithName("RenderQuickReply");

        return app;
    }
}
