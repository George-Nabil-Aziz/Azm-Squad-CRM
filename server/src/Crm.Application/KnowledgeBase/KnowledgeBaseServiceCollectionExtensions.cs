using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application.KnowledgeBase;

public static class KnowledgeBaseServiceCollectionExtensions
{
    /// <summary>The knowledge base services (validators are registered by the assembly scan in AddApplication).</summary>
    public static IServiceCollection AddKnowledgeBase(this IServiceCollection services)
    {
        services.AddScoped<IKbCategoryService, KbCategoryService>();
        services.AddScoped<IKbArticleService, KbArticleService>();
        return services;
    }
}
