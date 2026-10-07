using Crm.Application.Portal;
using Microsoft.Extensions.Configuration;
using Crm.Application.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Crm.Infrastructure.Portal;

public static class PortalServiceCollectionExtensions
{
    /// <summary>
    /// The portal repositories and settings (<c>Portal:*</c>, read lazily from the final configuration so the test host can set them).
    /// Invalid values (zero or negative days) fall back to the defaults.
    /// </summary>
    public static IServiceCollection AddPortalInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPortalAccountRepository, PortalAccountRepository>();
        services.AddScoped<ISurveyRepository, SurveyRepository>();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IConfiguration>().GetSection(PortalOptions.SectionName).Get<PortalOptions>()
                          ?? new PortalOptions();
            var defaults = new PortalOptions();
            if (options.ReopenWindowDays <= 0)
            {
                options.ReopenWindowDays = defaults.ReopenWindowDays;
            }

            if (options.SurveyValidDays <= 0)
            {
                options.SurveyValidDays = defaults.SurveyValidDays;
            }

            var env = provider.GetRequiredService<IHostEnvironment>().EnvironmentName;
            options.LogLoginCodes = DemoAccounts.IsEnabled(env, options.LogLoginCodes ? "true" : "false")
                                    || string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
            return options;
        });
        return services;
    }
}
