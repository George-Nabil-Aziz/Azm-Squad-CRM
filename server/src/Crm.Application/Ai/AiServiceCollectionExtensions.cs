using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application.Ai;

public static class AiServiceCollectionExtensions
{
    /// <summary>The AI feature services (the provider <see cref="IAiTextService"/> is registered by Infrastructure).</summary>
    public static IServiceCollection AddAi(this IServiceCollection services)
    {
        services.AddScoped<ITicketSummaryService, TicketSummaryService>();
        return services;
    }
}
