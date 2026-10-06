using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Sla;
using Crm.Infrastructure.Jobs;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Sla;

/// <summary>CRM-21 AC 5: the job is scheduled every minute, but Hangfire never starts in the Testing host.</summary>
public class SlaJobScheduleTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public void RecurringJobs_RegisterTheSlaMonitorEveryMinute()
    {
        var manager = new CapturingRecurringJobManager();

        RecurringJobs.Register(manager);

        Assert.Contains(manager.Added, a => a.Id == "task-reminders" && a.Job.Type == typeof(Crm.Application.Tasks.TaskReminderJob));
        var (id, job, cron) = Assert.Single(manager.Added, a => a.Id == "sla-monitor");
        Assert.Equal("sla-monitor", id);
        Assert.Equal("* * * * *", cron);
        Assert.Equal(typeof(SlaMonitorJob), job.Type);
        Assert.Equal(nameof(SlaMonitorJob.RunAsync), job.Method.Name);
    }

    [Fact]
    public void TestingHost_DoesNotStartHangfire()
    {
        Assert.Null(factory.Services.GetService<IRecurringJobManager>());
        Assert.Null(factory.Services.GetService<JobStorage>());
    }

    [Fact]
    public void AddCrmJobs_WithoutConnectionStringOrWhenDisabled_AddsNothing()
    {
        var empty = new ConfigurationBuilder().Build();
        var disabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Crm"] = "Server=x;Database=y",
            ["Jobs:Enabled"] = "false",
        }).Build();

        Assert.False(new ServiceCollection().AddCrmJobs(empty));
        Assert.False(new ServiceCollection().AddCrmJobs(disabled));
    }

    private sealed class CapturingRecurringJobManager : IRecurringJobManager
    {
        public List<(string Id, Job Job, string Cron)> Added { get; } = [];

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options) =>
            Added.Add((recurringJobId, job, cronExpression));

        public void Trigger(string recurringJobId)
        {
        }

        public void RemoveIfExists(string recurringJobId)
        {
        }
    }
}
