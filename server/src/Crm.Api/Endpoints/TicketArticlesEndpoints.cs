using Crm.Application.Auth;
using Crm.Application.KnowledgeBase;

namespace Crm.Api.Endpoints;

public static class TicketArticlesEndpoints
{
    public static IEndpointRouteBuilder MapTicketArticlesEndpoints(this IEndpointRouteBuilder app)
    {
        // Listing needs tickets.view; inserting an article into a reply needs tickets.manage (writing replies) and kb.view.
        var group = app.MapGroup("/api/tickets/{id:guid}/articles").RequireAuthorization(Permissions.TicketsView);

        group.MapGet("", async (Guid id, ITicketArticleService articles, CancellationToken cancellationToken) =>
                Results.Ok(await articles.ListAsync(id, cancellationToken)))
            .WithName("ListTicketArticles");

        group.MapPost("", async (Guid id, LinkArticleRequest request, ITicketArticleService articles,
                    CancellationToken cancellationToken) =>
                Results.Created($"/api/tickets/{id}/articles", await articles.LinkAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.TicketsManage, Permissions.KbView)
            .WithName("LinkTicketArticle");

        return app;
    }
}
