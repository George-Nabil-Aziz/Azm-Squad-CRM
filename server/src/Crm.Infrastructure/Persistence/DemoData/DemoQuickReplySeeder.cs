using Crm.Application.Auth;
using Crm.Domain.QuickReplies;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.DemoData;

/// <summary>
/// Development-only demo quick replies (CRM-32): <see cref="Shared"/> shared replies (owned by superadmin@crm.com) and
/// <see cref="Personal"/> personal ones for agent@crm.com, each in English and Arabic with the placeholders. Same switches as
/// <see cref="DemoDataSeeder"/>; idempotent per reply (owner + shortcut), so it never duplicates and never touches replies the
/// developer made. Nothing is sent.
/// </summary>
public sealed class DemoQuickReplySeeder(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DemoQuickReplySeeder> logger)
{
    private sealed record Def(string Title, string Shortcut, string Body);

    private static readonly Def[] Shared =
    [
        new("Greeting / ترحيب", "/greeting",
            "Hello {{customer.name}}, this is {{agent.name}}. Thank you for contacting us about {{ticket.number}}. How can I help?\n\nمرحبا {{customer.name}}، معك {{agent.name}}. شكرا لتواصلك معنا بخصوص {{ticket.number}}. كيف يمكنني مساعدتك؟"),
        new("Ask for the order number / طلب رقم الطلب", "/order",
            "Hello {{customer.name}}, to look into \"{{ticket.subject}}\" could you please send me your order number?\n\nمرحبا {{customer.name}}، لمراجعة \"{{ticket.subject}}\" نرجو إرسال رقم الطلب."),
        new("Apology for the delay / اعتذار عن التأخير", "/sorry",
            "Dear {{customer.name}}, we apologise for the delay on {{ticket.number}}. We are working on it and will update you soon. {{agent.name}}\n\nعزيزي {{customer.name}}، نعتذر عن التأخير في {{ticket.number}}. نعمل على الموضوع وسنوافيك بالتحديث قريبا. {{agent.name}}"),
        new("Escalation notice / إشعار التصعيد", "/escalated",
            "Hello {{customer.name}}, your request {{ticket.number}} has been escalated to a senior colleague who will contact you shortly. {{agent.name}}\n\nمرحبا {{customer.name}}، تم تصعيد طلبك {{ticket.number}} إلى زميل أعلى خبرة وسيتواصل معك قريبا. {{agent.name}}"),
        new("Resolution confirmation / تأكيد الحل", "/resolved",
            "Hello {{customer.name}}, we have resolved \"{{ticket.subject}}\" ({{ticket.number}}). Please let us know if anything is still wrong. {{agent.name}}\n\nمرحبا {{customer.name}}، تم حل \"{{ticket.subject}}\" ({{ticket.number}}). أخبرنا إن بقيت أي مشكلة. {{agent.name}}"),
        new("Closing and thanks / إغلاق وشكر", "/thanks",
            "Thank you {{customer.name}} for your patience. We are closing {{ticket.number}}; you can reply any time to reopen it. Best regards, {{agent.name}}\n\nشكرا لك {{customer.name}} على صبرك. سنغلق {{ticket.number}} ويمكنك الرد في أي وقت لإعادة فتحه. مع التحية، {{agent.name}}"),
        new("Request a screenshot / طلب لقطة شاشة", "/screenshot",
            "Hello {{customer.name}}, could you send a screenshot of the problem with {{ticket.number}}? It will help us a lot. {{agent.name}}\n\nمرحبا {{customer.name}}، هل يمكنك إرسال لقطة شاشة للمشكلة في {{ticket.number}}؟ ستساعدنا كثيرا. {{agent.name}}"),
        new("Follow-up / متابعة", "/followup",
            "Hello {{customer.name}}, just checking that \"{{ticket.subject}}\" ({{ticket.number}}) is working for you now. {{agent.name}}\n\nمرحبا {{customer.name}}، أتأكد أن \"{{ticket.subject}}\" ({{ticket.number}}) يعمل معك الآن. {{agent.name}}"),
    ];

    private static readonly Def[] Personal =
    [
        new("My signature / توقيعي", "/sig",
            "Best regards,\n{{agent.name}}\nAZM Squad Customer Support\n\nمع أفضل التحيات،\n{{agent.name}}\nدعم عملاء AZM Squad"),
        new("Call back later / سأتصل لاحقا", "/callback",
            "Hello {{customer.name}}, I will call you back about {{ticket.number}} later today. {{agent.name}}\n\nمرحبا {{customer.name}}، سأتصل بك بخصوص {{ticket.number}} لاحقا اليوم. {{agent.name}}"),
        new("Waiting for your reply / بانتظار ردك", "/waiting",
            "Hello {{customer.name}}, we are waiting for your reply on \"{{ticket.subject}}\" to continue. {{agent.name}}\n\nمرحبا {{customer.name}}، بانتظار ردك على \"{{ticket.subject}}\" لنتمكن من المتابعة. {{agent.name}}"),
    ];

    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var seeder = ActivatorUtilities.CreateInstance<DemoQuickReplySeeder>(services);
        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<DemoQuickReplySeeder>>().LogError(exception, "Seeding the development demo quick replies failed.");
        }
    }

    /// <summary>Returns the number of replies added.</summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>(DemoDataSeeder.FlagKey))
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var added = 0;
        var admin = await userManager.FindByEmailAsync(DemoAccounts.SuperAdminEmail) ?? await userManager.FindByEmailAsync(DemoAccounts.AdminEmail);
        if (admin is not null)
        {
            added += await EnsureAsync(admin.Id, Shared, true, now, cancellationToken);
        }

        if (await userManager.FindByEmailAsync(DemoAccounts.AgentEmail) is { } agent)
        {
            added += await EnsureAsync(agent.Id, Personal, false, now, cancellationToken);
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} development demo quick replies.", added);
        }

        return added;
    }

    private async Task<int> EnsureAsync(Guid owner, Def[] defs, bool shared, DateTime now, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var def in defs)
        {
            if (await db.QuickReplies.AnyAsync(r => r.OwnerId == owner && r.Shortcut == def.Shortcut, cancellationToken))
            {
                continue;
            }

            db.QuickReplies.Add(QuickReply.Create(owner, def.Title, def.Shortcut, def.Body, shared, now));
            count++;
        }

        return count;
    }
}
