using Crm.Application.Auth;
using Crm.Domain.Tasks;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.DemoData;

/// <summary>
/// Development-only: tops every demo staff account up to exactly <see cref="TasksPerUser"/> tasks (never more, never deletes).
/// Independent of the main demo data marker so it also works on a database that already has the demo data. Under the same
/// switches as <see cref="DemoDataSeeder"/>. Past-due and done tasks are created through the domain with an earlier creation
/// time (due after creation), so no invariant is bypassed. No notifications are created.
/// </summary>
public sealed class DemoTaskSeeder(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DemoTaskSeeder> logger)
{
    public const int TasksPerUser = 10;

    public static readonly string[] AccountEmails =
        [DemoAccounts.SuperAdminEmail, DemoAccounts.AdminEmail, DemoAccounts.SupervisorEmail, DemoAccounts.AgentEmail];

    private enum Slot { Overdue, Today, Next, Done }

    private sealed record Template(Slot Slot, double Hours, string Title, string Description, bool Linked);

    // Interleaved so that any suffix (used when a user already has some tasks) keeps a mix of states.
    // Per 10: 2 overdue, 3 due today, 3 in the next days, 2 done.
    private static readonly Template[] Templates =
    [
        new(Slot.Overdue, -26, "Call the customer back about the unpaid invoice", "The customer asked for a corrected invoice and a call back.", true),
        new(Slot.Today, 2, "متابعة العميل بخصوص التذكرة المفتوحة", "التأكد من أن العميل استلم الرد وأن المشكلة حُلّت.", true),
        new(Slot.Next, 30, "Review the password reset knowledge base article", "Check the steps are still correct after the last release.", false),
        new(Slot.Done, -70, "إرسال ملخص الفواتير للعميل", "ملخص فواتير الشهر الماضي للعميل المهم.", false),
        new(Slot.Today, 5, "Check the escalation with the logistics team", "Confirm the delivery date and update the customer.", true),
        new(Slot.Next, 52, "تحضير ملخص اتفاقية مستوى الخدمة الأسبوعي", "تجميع التذاكر القريبة من الخرق وإرسال الملخص للمشرف.", false),
        new(Slot.Overdue, -5, "مراجعة التذاكر القريبة من خرق الاتفاقية", "ترتيب التذاكر حسب الأولوية وتحديد المتأخر منها.", false),
        new(Slot.Today, 8, "Follow up with the VIP customer about the refund", "Refund was approved; confirm it reached the customer.", true),
        new(Slot.Next, 100, "تحديث مقال قاعدة المعرفة عن حالة الشحنة", "إضافة الأسئلة الشائعة الجديدة وروابط التتبع.", false),
        new(Slot.Done, -52, "Prepare the weekly SLA summary", "Response and resolution rates for the supervisor meeting.", false),
    ];

    /// <summary>Startup entry point: a failure is logged and never stops the API from starting.</summary>
    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var seeder = ActivatorUtilities.CreateInstance<DemoTaskSeeder>(services);
        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<DemoTaskSeeder>>().LogError(exception, "Seeding the development demo tasks failed.");
        }
    }

    /// <summary>Returns the number of tasks added.</summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>(DemoDataSeeder.FlagKey))
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tickets = await db.Tickets.AsNoTracking().OrderByDescending(t => t.CreatedAt).Take(40)
            .Select(t => new { t.Id, t.AssigneeId }).ToListAsync(cancellationToken);
        var added = 0;
        foreach (var email in AccountEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                continue;
            }

            var existing = await db.Tasks.CountAsync(t => t.OwnerId == user.Id, cancellationToken);
            var mine = tickets.Where(t => t.AssigneeId == user.Id).Select(t => t.Id).Concat(tickets.Select(t => t.Id)).Distinct().ToList();
            var linkIndex = 0;
            for (var i = existing; i < TasksPerUser; i++)
            {
                var template = Templates[i];
                Guid? ticketId = template.Linked && mine.Count > 0 ? mine[linkIndex++ % mine.Count] : null;
                db.Tasks.Add(Build(user.Id, template, ticketId, now));
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} development demo tasks (up to {Per} per demo staff account).", added, TasksPerUser);
        }

        return added;
    }

    private static WorkTask Build(Guid ownerId, Template template, Guid? ticketId, DateTime now)
    {
        var due = now.AddHours(template.Hours);
        switch (template.Slot)
        {
            case Slot.Overdue:
                return WorkTask.Create(ownerId, template.Title, template.Description, due, ticketId, due.AddDays(-2));
            case Slot.Done:
                var task = WorkTask.Create(ownerId, template.Title, template.Description, due, ticketId, due.AddDays(-3));
                task.MarkDone(due.AddHours(-2));
                return task;
            default:
                return WorkTask.Create(ownerId, template.Title, template.Description, due, ticketId, now);
        }
    }
}
