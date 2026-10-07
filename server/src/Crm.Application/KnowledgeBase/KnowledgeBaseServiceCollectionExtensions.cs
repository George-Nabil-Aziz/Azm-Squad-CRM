using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application.KnowledgeBase;

public static class KnowledgeBaseServiceCollectionExtensions
{
    /// <summary>The knowledge base services (validators are registered by the assembly scan in AddApplication).</summary>
    public static IServiceCollection AddKnowledgeBase(this IServiceCollection services)
    {
        services.AddScoped<IKbCategoryService, KbCategoryService>();
        services.AddScoped<IKbArticleService, KbArticleService>();
        services.AddScoped<IKbFaqService, KbFaqService>();
        services.AddScoped<IKbSearchService, KbSearchService>();
        services.AddScoped<ITicketArticleService, TicketArticleService>();
        services.AddScoped<IKbRetriever, KbRetriever>();
        return services;
    }
}
