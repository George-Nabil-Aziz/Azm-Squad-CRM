using Crm.Application.KnowledgeBase;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.KnowledgeBase;

public static class KnowledgeBaseServiceCollectionExtensions
{
    /// <summary>The knowledge base repositories.</summary>
    public static IServiceCollection AddKnowledgeBaseStorage(this IServiceCollection services)
    {
        services.AddScoped<IKbCategoryRepository, KbCategoryRepository>();
        services.AddScoped<IKbArticleRepository, KbArticleRepository>();
        services.AddScoped<IKbFaqRepository, KbFaqRepository>();
        services.AddScoped<IKbSearchRepository, KbSearchRepository>();
        return services;
    }
}
