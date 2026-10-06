using Crm.Application.KnowledgeBase;

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

        return app;
    }
}
