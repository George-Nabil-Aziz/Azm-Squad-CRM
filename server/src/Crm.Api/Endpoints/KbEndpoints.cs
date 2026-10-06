using Crm.Application.Auth;
using Crm.Application.KnowledgeBase;

namespace Crm.Api.Endpoints;

public static class KbEndpoints
{
    public static IEndpointRouteBuilder MapKbEndpoints(this IEndpointRouteBuilder app)
    {
        // Reading needs kb.view (published content only unless the user also has kb.manage); writing needs kb.manage.
        var kb = app.MapGroup("/api/kb").RequireAuthorization(Permissions.KbView);

        var categories = kb.MapGroup("/categories");
        categories.MapGet("", async (IKbCategoryService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(cancellationToken)))
            .WithName("ListKbCategories");
        categories.MapPost("", async (KbCategoryRequest request, IKbCategoryService service, CancellationToken cancellationToken) =>
            {
                var category = await service.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/kb/categories/{category.Id}", category);
            })
            .RequireAuthorization(Permissions.KbManage)
            .WithName("CreateKbCategory");
        categories.MapPut("/{id:guid}", async (Guid id, KbCategoryRequest request, IKbCategoryService service,
                    CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.KbManage)
            .WithName("UpdateKbCategory");
        categories.MapDelete("/{id:guid}", async (Guid id, IKbCategoryService service, CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.KbManage)
            .WithName("DeleteKbCategory");

        var articles = kb.MapGroup("/articles");
        articles.MapGet("", async ([AsParameters] ListKbArticlesQuery query, IKbArticleService service,
                    CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(query, cancellationToken)))
            .WithName("ListKbArticles");
        articles.MapGet("/{id:guid}", async (Guid id, IKbArticleService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(id, cancellationToken)))
            .WithName("GetKbArticle");
        articles.MapPost("", async (KbArticleRequest request, IKbArticleService service, CancellationToken cancellationToken) =>
            {
                var article = await service.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/kb/articles/{article.Id}", article);
            })
            .RequireAuthorization(Permissions.KbManage)
            .WithName("CreateKbArticle");
        articles.MapPut("/{id:guid}", async (Guid id, KbArticleRequest request, IKbArticleService service,
                    CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.KbManage)
            .WithName("UpdateKbArticle");
        articles.MapPost("/{id:guid}/publish", async (Guid id, IKbArticleService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.PublishAsync(id, cancellationToken)))
            .RequireAuthorization(Permissions.KbManage)
            .WithName("PublishKbArticle");
        articles.MapPost("/{id:guid}/unpublish", async (Guid id, IKbArticleService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.UnpublishAsync(id, cancellationToken)))
            .RequireAuthorization(Permissions.KbManage)
            .WithName("UnpublishKbArticle");
        articles.MapDelete("/{id:guid}", async (Guid id, IKbArticleService service, CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.KbManage)
            .WithName("DeleteKbArticle");

        return app;
    }
}
