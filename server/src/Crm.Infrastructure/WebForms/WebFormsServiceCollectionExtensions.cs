using Crm.Application.WebForms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.WebForms;

public static class WebFormsInfrastructureExtensions
{
    /// <summary>Web form settings (<c>WebForms</c> section, read lazily) and the captcha provider client.</summary>
    public static IServiceCollection AddWebFormsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
            provider.GetRequiredService<IConfiguration>().GetSection(WebFormOptions.SectionName).Get<WebFormOptions>() ?? new WebFormOptions());
        services.AddHttpClient<ICaptchaVerifier, HttpCaptchaVerifier>(client => client.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }
}
