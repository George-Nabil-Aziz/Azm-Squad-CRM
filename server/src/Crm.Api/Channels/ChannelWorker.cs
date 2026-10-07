using Crm.Application.Channels;
using Crm.Application.Integrations;
using Crm.Infrastructure.Channels.Email;

namespace Crm.Api.Channels;

/// <summary>
/// Recurring channel work (every <c>Channels:WorkerIntervalSeconds</c>, default 60): retry failed outgoing messages and
/// read new emails. Each run uses its own DI scope; errors are logged and the next run tries again. Not started in the
/// <c>Testing</c> environment — tests call the job methods directly. (Moves to Hangfire recurring jobs once the
/// solution has Hangfire.)
/// </summary>
public sealed class ChannelWorker(
    IServiceScopeFactory scopeFactory, IConfiguration configuration, IHostEnvironment environment, ILogger<ChannelWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var seconds = configuration.GetValue("Channels:WorkerIntervalSeconds", 60);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, seconds)));
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown: the host cancelled the worker. Ending quietly keeps a shutdown (or a failed start,
            // e.g. the port is already in use) from being reported as a crashed background service.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var (name, job) in Jobs())
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await job(scope.ServiceProvider, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Channel job {Job} failed.", name);
            }
        }
    }

    private static IEnumerable<(string Name, Func<IServiceProvider, CancellationToken, Task> Run)> Jobs()
    {
        yield return ("retry-outbound", (services, ct) => services.GetRequiredService<IChannelSender>().RetryDueAsync(ct));
        yield return ("webhook-deliveries", (services, ct) => services.GetRequiredService<WebhookDeliveryJob>().RunAsync(ct));
        yield return ("poll-email", (services, ct) => services.GetRequiredService<EmailInboxPoller>().PollAsync(ct));
    }
}
