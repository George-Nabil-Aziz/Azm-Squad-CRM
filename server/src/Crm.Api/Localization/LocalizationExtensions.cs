using System.Globalization;
using Crm.Application.Common.Localization;
using Microsoft.AspNetCore.Localization;

namespace Crm.Api.Localization;

public static class LocalizationExtensions
{
    /// <summary>
    /// UI language per request from the Accept-Language header: "ar" (also "ar-SA", "ar-EG", …) or "en";
    /// anything else → "en". Only the UI culture changes (messages); the formatting culture stays "en",
    /// so dates and numbers are never formatted with the Arabic (Hijri) calendar by accident.
    /// </summary>
    public static IServiceCollection AddCrmLocalization(this IServiceCollection services)
    {
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(LocalizedText.English);
            options.SupportedCultures = [new CultureInfo(LocalizedText.English)];
            options.SupportedUICultures = [.. LocalizedText.SupportedLanguages.Select(language => new CultureInfo(language))];
            options.FallBackToParentUICultures = true;
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
            options.ApplyCurrentCultureToResponseHeaders = true;
        });
        return services;
    }

    /// <summary>Must run before UseCrmErrorHandling, so ProblemDetails are written in the request language.</summary>
    public static WebApplication UseCrmLocalization(this WebApplication app)
    {
        app.UseRequestLocalization();
        return app;
    }
}
