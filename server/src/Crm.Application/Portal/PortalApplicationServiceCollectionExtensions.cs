using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Application.Portal;

public static class PortalApplicationServiceCollectionExtensions
{
    /// <summary>The customer portal services.</summary>
    public static IServiceCollection AddPortal(this IServiceCollection services)
    {
        services.TryAddSingleton<IPortalCodeGenerator, RandomPortalCodeGenerator>();
        services.AddScoped<IPortalAuthService, PortalAuthService>();
        return services;
    }
}
