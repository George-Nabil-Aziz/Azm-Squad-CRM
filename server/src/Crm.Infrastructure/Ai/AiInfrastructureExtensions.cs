using Crm.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Ai;

public static class AiInfrastructureExtensions
{
    /// <summary>
    /// The AI provider and AI storage. <c>Ai:*</c> is read lazily from the final configuration, so the test host can set it; a
    /// missing key only makes <see cref="IAiTextService.IsConfigured"/> false, it never stops the app.
    /// </summary>
    public static IServiceCollection AddAiInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IConfiguration>().GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
            var defaults = new AiOptions();
            if (options.TimeoutSeconds <= 0)
            {
                options.TimeoutSeconds = defaults.TimeoutSeconds;
            }

            if (options.ConfidenceThreshold is <= 0 or > 1)
            {
                options.ConfidenceThreshold = defaults.ConfidenceThreshold;
            }

            return options;
        });
        services.AddHttpClient<IAiTextService, AnthropicTextService>((provider, client) =>
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<AiOptions>().TimeoutSeconds));
        services.AddScoped<ITicketSummaryRepository, TicketSummaryRepository>();
        services.AddScoped<ITicketAiClassificationRepository, AiClassificationRepository>();
        return services;
    }
}
