using Crm.Application.Common.Paging;
using Crm.Application.KnowledgeBase;
using Crm.Application.Portal;

namespace Crm.Api.Endpoints;

/// <summary>
/// The public knowledge base of the customer portal: anonymous (customers need not sign in to read help content), published
/// content only, in the request language (Accept-Language).
/// </summary>
public static class PortalKbEndpoints
{
    public static IEndpointRouteBuilder MapPortalKbEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/kb").AllowAnonymous();

        group.MapGet("/faqs", async (IKbFaqService faqs, CancellationToken cancellationToken) =>
                Results.Ok(await faqs.ListPublishedAsync(cancellationToken)))
            .WithName("ListPortalFaqs");

        group.MapGet("/search", async (string? q, IKbSearchService search, CancellationToken cancellationToken) =>
                Results.Ok(await search.SearchAsync(q, cancellationToken)))
            .WithName("SearchPortalKb");

        group.MapGet("/categories", async (IPortalKbService kb, CancellationToken cancellationToken) =>
                Results.Ok(await kb.ListCategoriesAsync(cancellationToken)))
            .WithName("ListPortalKbCategories");

        group.MapGet("/articles", async (Guid? categoryId, int? page, int? pageSize, IPortalKbService kb,
                    CancellationToken cancellationToken) =>
                Results.Ok(await kb.ListArticlesAsync(
                    categoryId, page ?? PagingDefaults.DefaultPage, pageSize ?? PagingDefaults.DefaultPageSize, cancellationToken)))
            .WithName("ListPortalKbArticles");

        group.MapGet("/articles/{id:guid}", async (Guid id, IPortalKbService kb, CancellationToken cancellationToken) =>
                Results.Ok(await kb.GetArticleAsync(id, cancellationToken)))
            .WithName("GetPortalKbArticle");

        // "Was this helpful?": no sign-in needed (the portal home is public).
        group.MapPost("/articles/{id:guid}/feedback", async (Guid id, PortalFeedbackRequest request, IPortalKbService kb,
                    CancellationToken cancellationToken) =>
                Results.Ok(await kb.RecordFeedbackAsync(id, request, cancellationToken)))
            .WithName("PortalKbArticleFeedback");

        return app;
    }
}
