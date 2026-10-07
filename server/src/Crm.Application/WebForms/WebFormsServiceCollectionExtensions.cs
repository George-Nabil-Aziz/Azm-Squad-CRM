using Crm.Application.Common.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Application.WebForms;

public static class WebFormsServiceCollectionExtensions
{
    /// <summary>Web form service and the shared in-memory rate limiter (CRM-55).</summary>
    public static IServiceCollection AddWebForms(this IServiceCollection services)
    {
        services.TryAddSingleton<IRateLimiter, FixedWindowRateLimiter>();
        services.AddScoped<IWebFormService, WebFormService>();
        return services;
    }
}
