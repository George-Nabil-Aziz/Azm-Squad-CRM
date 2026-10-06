using Crm.Application.Sla;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Jobs;

/// <summary>Hangfire wiring (SQL Server storage) for the recurring jobs. Never called in the Testing environment.</summary>
public static class JobsExtensions
{
    /// <summary>
    /// Adds Hangfire storage and server. Returns false (and adds nothing) when <c>Jobs:Enabled</c> is false or
    /// <c>ConnectionStrings:Crm</c> is missing, so missing configuration never crashes startup.
    /// </summary>
    public static bool AddCrmJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Crm");
        if (!configuration.GetValue("Jobs:Enabled", true) || string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString));
        services.AddHangfireServer();
        return true;
    }

    /// <summary>Registers the recurring jobs after startup; a database that is down is logged, not fatal.</summary>
    public static void RegisterCrmRecurringJobs(this IServiceProvider services)
    {
        try
        {
            RecurringJobs.Register(services.GetRequiredService<IRecurringJobManager>());
        }
        catch (Exception exception)
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger("Crm.Jobs")
                .LogWarning(exception, "Recurring jobs were not registered (is the database reachable?).");
        }
    }
}

/// <summary>The recurring jobs of the CRM.</summary>
public static class RecurringJobs
{
    public const string SlaMonitorId = "sla-monitor";

    public static void Register(IRecurringJobManager manager) =>
        manager.AddOrUpdate<SlaMonitorJob>(SlaMonitorId, job => job.RunAsync(CancellationToken.None), Cron.Minutely());
}
