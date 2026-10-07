using System.Security.Cryptography;
using System.Text;
using Crm.Application.Auth;
using Crm.Application.Chat;
using Crm.Domain.Chat;
using Crm.Domain.Customers;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.DemoData;

/// <summary>
/// Development-only demo live chats (CRM-56): <see cref="EndedCount"/> ended chats over the last two weeks, each saved as a Chat
/// ticket with the transcript built by <see cref="ChatTranscript"/> (what the real "end chat" flow stores), plus
/// <see cref="OpenCount"/> chats the staff console lists right now (two waiting, one active with agent@crm.com). Visitors are the
/// demo customers. Same switches as <see cref="DemoDataSeeder"/>; own marker: any chat from a visitor whose email ends with
/// <see cref="DemoDataCatalog.CustomerEmailDomain"/>. Nothing is sent: rows are inserted directly, no notifier or webhook runs.
/// </summary>
public sealed class DemoChatSeeder(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DemoChatSeeder> logger)
{
    public const int EndedCount = 10;
    public const int OpenCount = 3;

    private static readonly string[] AgentEmails = [DemoAccounts.AgentEmail, "sara@crm.com", "omar@crm.com", "layla@crm.com"];

    // v| = visitor, a| = agent; {agent} is the agent's name.
    private static readonly string[][] ScriptsEn =
    [
        ["v|Hi, my order has not arrived yet and it was due yesterday.", "a|Hello, this is {agent}. Sorry for the delay, could you share the order number?", "v|It is 48213.", "a|Thank you. I can see it is with the courier; delivery is scheduled for tomorrow morning.", "v|Great, please make sure it is not delayed again.", "a|I added a note to prioritise it. Anything else I can help with?", "v|No, that is all. Thanks!"],
        ["v|I cannot sign in to my account, the reset email never arrives.", "a|Hi, {agent} here. Which email address did you use?", "v|The one I am chatting with.", "a|I resent the reset link now, please also check the spam folder.", "v|Got it, it worked. Thank you!", "a|Glad to help. Have a nice day."],
        ["v|I was charged twice for the same invoice this month.", "a|I am sorry about that. Let me check, this is {agent} from billing support.", "v|Invoice 90177, both charges are on the 3rd.", "a|Confirmed, the second charge is a duplicate. I am starting a refund now.", "v|How long will it take?", "a|3 to 5 business days to reach your bank.", "v|OK, thank you for the quick help.", "a|You are welcome. You will get an email confirmation shortly."],
        ["v|Can I change the delivery address of my order?", "a|Sure, {agent} speaking. Has the order shipped?", "v|Not yet, I think.", "a|Right, it is still in preparation. Please send me the new address.", "v|Olaya Street 12, Riyadh.", "a|Updated. Anything else?", "v|That is all, thanks."],
        ["v|The app keeps closing when I open my invoices.", "a|Hello, {agent} from technical support. Which phone and app version?", "v|Android 14, version 3.2.", "a|Thanks. Please update to 3.2.1 which fixes this crash.", "v|Updating now... it works!", "a|Perfect. If it happens again, write to us any time."],
    ];

    private static readonly string[][] ScriptsAr =
    [
        ["v|مرحبا، طلبي لم يصل حتى الآن وكان من المفترض أن يصل أمس.", "a|أهلا بك، معك {agent}. نعتذر عن التأخير، هل يمكنك تزويدي برقم الطلب؟", "v|رقم الطلب 48213.", "a|شكرا لك. الطلب مع شركة الشحن والتسليم مجدول صباح الغد.", "v|ممتاز، أرجو التأكد ألا يتأخر مرة أخرى.", "a|أضفت ملاحظة بإعطائه الأولوية. هل هناك شيء آخر؟", "v|لا، شكرا لك."],
        ["v|لا أستطيع تسجيل الدخول إلى حسابي ولا تصلني رسالة إعادة التعيين.", "a|أهلا، أنا {agent}. ما هو البريد الإلكتروني الذي استخدمته؟", "v|نفس البريد الذي أتحدث منه.", "a|أرسلت رابط إعادة التعيين الآن، تأكد أيضا من مجلد الرسائل غير المرغوبة.", "v|وصلتني وتم الأمر، شكرا جزيلا!", "a|على الرحب والسعة، يوما سعيدا."],
        ["v|تم خصم مبلغ الفاتورة مرتين هذا الشهر.", "a|نعتذر عن ذلك، معك {agent} من دعم الفواتير. هل لديك رقم الفاتورة؟", "v|الفاتورة 90177 والخصمان في يوم 3.", "a|تأكدت أن الخصم الثاني مكرر وسأبدأ إجراء الاسترجاع الآن.", "v|كم يستغرق ذلك؟", "a|من 3 إلى 5 أيام عمل حتى تصل إلى حسابك البنكي.", "v|حسنا، شكرا على سرعة المساعدة.", "a|العفو، سيصلك تأكيد بالبريد الإلكتروني قريبا."],
        ["v|هل يمكنني تغيير عنوان التوصيل لطلبي؟", "a|بالتأكيد، معك {agent}. هل تم شحن الطلب؟", "v|لا أظن ذلك.", "a|الطلب ما زال قيد التجهيز، أرسل لي العنوان الجديد من فضلك.", "v|شارع العليا 12، الرياض.", "a|تم التحديث. هل أستطيع مساعدتك بشيء آخر؟", "v|لا، شكرا."],
        ["v|التطبيق يغلق عند فتح صفحة الفواتير.", "a|مرحبا، {agent} من الدعم الفني. ما نوع الهاتف وإصدار التطبيق؟", "v|أندرويد 14 والإصدار 3.2.", "a|شكرا. من فضلك حدّث إلى 3.2.1 فهو يعالج هذه المشكلة.", "v|جاري التحديث... اشتغل التطبيق!", "a|ممتاز، إذا تكررت المشكلة تواصل معنا في أي وقت."],
    ];

    private static readonly string[][] OpenWaiting =
    [
        ["v|Hello, is anyone there? I need help with a refund."],
        ["v|مرحبا، أحتاج مساعدة بخصوص فاتورة لم أستلمها."],
    ];

    private static readonly string[] OpenActiveEn =
    [
        "v|Hi, I would like to know the status of my ticket.",
        "a|Hello, {agent} here. Let me look it up for you.",
        "v|Thank you, I am waiting.",
        "a|It is being reviewed by the technical team, I will update you shortly.",
    ];

    private static readonly string[] OpenActiveAr =
    [
        "v|مرحبا، أريد معرفة حالة التذكرة الخاصة بي.",
        "a|أهلا بك، معك {agent}. دقيقة وسأراجعها لك.",
        "v|شكرا، بانتظارك.",
        "a|التذكرة قيد المراجعة لدى الفريق الفني وسأوافيك بالتحديث قريبا.",
    ];

    /// <summary>Startup entry point: a failure is logged and never stops the API from starting.</summary>
    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var seeder = ActivatorUtilities.CreateInstance<DemoChatSeeder>(services);
        try
        {
            await seeder.SeedAsync(cancellationToken);
            await seeder.TopUpAsync(cancellationToken); // own marker: also tops up a database that already has the base chats
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<DemoChatSeeder>>().LogError(exception, "Seeding the development demo chats failed.");
        }
    }

    /// <summary>Returns the number of chats added.</summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>(DemoDataSeeder.FlagKey))
        {
            return 0;
        }

        if (await db.ChatSessions.AnyAsync(s => s.VisitorEmail.EndsWith(DemoDataCatalog.CustomerEmailDomain), cancellationToken))
        {
            return 0;
        }

        var customers = await db.Customers.Where(c => c.Email != null && c.Email.EndsWith(DemoDataCatalog.CustomerEmailDomain))
            .OrderBy(c => c.Email).Take(EndedCount + OpenCount).ToListAsync(cancellationToken);
        var agents = new List<(Guid Id, string Name)>();
        foreach (var email in AgentEmails)
        {
            if (await userManager.FindByEmailAsync(email) is { } user)
            {
                agents.Add((user.Id, string.IsNullOrWhiteSpace(user.FullName) ? email : user.FullName));
            }
        }

        if (customers.Count < EndedCount + OpenCount || agents.Count == 0)
        {
            logger.LogWarning("Demo chats were not created: they need the demo customers and at least one demo agent.");
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rnd = new Random(20260611);
        var policy = await db.SlaPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Priority == TicketPriority.Mid, cancellationToken);
        if (policy is null)
        {
            var (priority, response, resolution) = SlaPolicy.Defaults.First(d => d.Priority == TicketPriority.Mid);
            policy = SlaPolicy.Create(priority, response, resolution, now); // in memory only, never saved
        }

        var number = ((await db.Tickets.IgnoreQueryFilters().MaxAsync(t => (int?)t.Number, cancellationToken)) ?? 0) + 1;

        for (var i = 0; i < EndedCount; i++)
        {
            // agent@crm.com (index 0) takes every third chat
            var agent = i % 3 == 0 ? agents[0] : agents[i % agents.Count];
            var started = now.Date.AddDays(-(1 + (i * 13 / EndedCount))).AddHours(6 + rnd.Next(0, 9)).AddMinutes(rnd.Next(60));
            AddEnded(customers[i], agent, Script(customers[i], i, rnd), started, policy, number++);
        }

        AddOpen(customers[EndedCount], null, OpenWaiting[0], now.AddMinutes(-4));
        AddOpen(customers[EndedCount + 1], null, OpenWaiting[1], now.AddMinutes(-2));
        var activeCustomer = customers[EndedCount + 2];
        AddOpen(activeCustomer, agents[0], IsArabic(activeCustomer.Name) ? OpenActiveAr : OpenActiveEn, now.AddMinutes(-9));

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Ended} ended and {Open} open demo chats.", EndedCount, OpenCount);
        return EndedCount + OpenCount;
    }

    private static readonly string[] TopUpWaitingAr =
    [
        "v|السلام عليكم، هل يوجد أحد؟ أحتاج مساعدة في تغيير بيانات حسابي.",
        "v|أنتظر منذ دقائق، شكرا لكم.",
    ];

    private static readonly string[] TopUpActiveEn =
    [
        "v|Hello, my invoice shows the wrong company name.",
        "a|Hi, {agent} here. I can fix that, which invoice number is it?",
        "v|It is invoice 90312.",
        "a|Thanks, I am issuing a corrected copy now and will email it to you.",
    ];

    private static readonly string[] TopUpActiveAr =
    [
        "v|مرحبا، اسم الشركة في الفاتورة غير صحيح.",
        "a|أهلا بك، معك {agent}. أستطيع تصحيحه، ما رقم الفاتورة؟",
        "v|رقم الفاتورة 90312.",
        "a|شكرا لك، أصدر الآن نسخة مصححة وسأرسلها إلى بريدك.",
    ];

    /// <summary>
    /// Tops the demo chats up to 15 sessions (10 ended, 3 waiting, 2 active with two different agents) and adds two offline-form
    /// submissions (the form shown when no agent is online: a Chat-channel ticket without a session, one English, one Arabic).
    /// Needs the base demo chats; own marker: a chat of the 14th demo visitor (sessions) or a Chat ticket of the 16th (offline
    /// forms). Domain notes: a chat has only the states Waiting, Active and Ended and does not record who ended it, so there is
    /// no "ended by visitor / by agent" to seed. Returns the number of rows added (sessions + offline tickets).
    /// </summary>
    public async Task<int> TopUpAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>(DemoDataSeeder.FlagKey))
        {
            return 0;
        }

        var visitors = await db.Customers.Where(c => c.Email != null && c.Email.EndsWith(DemoDataCatalog.CustomerEmailDomain))
            .OrderBy(c => c.Email).Take(EndedCount + OpenCount + 4).ToListAsync(cancellationToken);
        if (visitors.Count < EndedCount + OpenCount + 4
            || !await db.ChatSessions.AnyAsync(s => s.VisitorEmail == visitors[EndedCount].Email, cancellationToken))
        {
            return 0; // the base demo chats come first
        }

        var agents = new List<(Guid Id, string Name)>();
        foreach (var email in AgentEmails)
        {
            if (await userManager.FindByEmailAsync(email) is { } user)
            {
                agents.Add((user.Id, string.IsNullOrWhiteSpace(user.FullName) ? email : user.FullName));
            }
        }

        if (agents.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var added = 0;
        var waitingCustomer = visitors[EndedCount + OpenCount];
        if (!await db.ChatSessions.AnyAsync(s => s.VisitorEmail == waitingCustomer.Email, cancellationToken))
        {
            var activeCustomer = visitors[EndedCount + OpenCount + 1];
            var second = agents[Math.Min(1, agents.Count - 1)];
            AddOpen(waitingCustomer, null, TopUpWaitingAr, now.AddMinutes(-6));
            AddOpen(activeCustomer, second, IsArabic(activeCustomer.Name) ? TopUpActiveAr : TopUpActiveEn, now.AddMinutes(-14));
            added += 2;
        }

        var englishOffline = visitors[EndedCount + OpenCount + 2];
        var arabicOffline = visitors[EndedCount + OpenCount + 3];
        if (!await db.Tickets.AnyAsync(t => t.Channel == TicketChannel.Chat && t.CustomerId == arabicOffline.Id, cancellationToken))
        {
            var policy = await db.SlaPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Priority == TicketPriority.Mid, cancellationToken);
            if (policy is null)
            {
                var (priority, response, resolution) = SlaPolicy.Defaults.First(d => d.Priority == TicketPriority.Mid);
                policy = SlaPolicy.Create(priority, response, resolution, now); // in memory only, never saved
            }

            var number = ((await db.Tickets.IgnoreQueryFilters().MaxAsync(t => (int?)t.Number, cancellationToken)) ?? 0) + 1;
            AddOffline(englishOffline, "Question about my subscription renewal (offline chat form)",
                "Hello, nobody was online in the chat. When does my subscription renew and can I change the plan before that?",
                now.AddHours(-9), policy, number++, null);
            AddOffline(arabicOffline, "استفسار عن موعد التسليم (نموذج المحادثة خارج الدوام)",
                "السلام عليكم، لم يكن هناك أحد في المحادثة. أرجو إفادتي بموعد تسليم طلبي وهل يمكن تغيير العنوان؟",
                now.AddHours(-30), policy, number, agents[0]);
            added += 2;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Topped up the demo live chats with {Count} more rows (chats and offline-form tickets).", added);
        }

        return added;
    }

    private void AddOffline(Customer customer, string subject, string message, DateTime created, SlaPolicy policy, int number, (Guid Id, string Name)? agent)
    {
        var ticket = Ticket.Create(customer.Id, subject, message, null, TicketPriority.Mid, TicketChannel.Chat, null, created);
        ticket.AssignNumber(number);
        ticket.AssignBranch(customer.BranchId);
        ticket.ApplySla(policy);
        if (agent is { } a)
        {
            ticket.AssignTo(a.Id, created.AddMinutes(20));
            ticket.ChangeStatus(TicketStatus.Open, created.AddMinutes(21));
        }

        db.Add(ticket);
        db.Add(TicketMessage.Inbound(ticket.Id, message, TicketChannel.Chat, null, created));
        db.Add(CustomerInteraction.Create(
            customer.Id, InteractionType.Ticket, InteractionEvents.TicketCreated, $"{ticket.DisplayNumber} {ticket.Subject}", ticket.Id, null, created));
    }

    private static bool IsArabic(string text) => text.Any(c => c is >= '؀' and <= 'ۿ');

    private static string[] Script(Customer customer, int index, Random rnd)
    {
        var pool = IsArabic(customer.Name) ? ScriptsAr : ScriptsEn;
        return pool[(index + rnd.Next(pool.Length)) % pool.Length];
    }

    private static ChatSession NewSession(Customer customer, DateTime started)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")))); // the token is never kept
        return ChatSession.Start(customer.Name, customer.Email!, hash, started);
    }

    private static void Write(ChatSession session, string[] script, string agentName, DateTime from, TimeSpan step)
    {
        var at = from;
        foreach (var line in script)
        {
            var visitor = line.StartsWith("v|", StringComparison.Ordinal);
            var text = line[2..].Replace("{agent}", agentName, StringComparison.Ordinal);
            session.AddMessage(visitor ? ChatSender.Visitor : ChatSender.Agent, visitor ? session.VisitorName : agentName, text, at);
            at = at.Add(step);
        }
    }

    private void AddEnded(Customer customer, (Guid Id, string Name) agent, string[] script, DateTime started, SlaPolicy policy, int number)
    {
        var session = NewSession(customer, started);
        session.Accept(agent.Id, agent.Name, started.AddMinutes(1));
        Write(session, script, agent.Name, started.AddMinutes(1), TimeSpan.FromSeconds(95));
        var ended = session.Messages.Max(m => m.SentAt).AddMinutes(1);
        session.End(ended);

        var transcript = ChatTranscript.Build(session.Messages);
        var ticket = Ticket.Create(customer.Id, ChatText.TicketSubject(session.VisitorName), transcript, null, TicketPriority.Mid, TicketChannel.Chat, null, ended);
        ticket.AssignNumber(number);
        ticket.AssignBranch(customer.BranchId);
        ticket.ApplySla(policy);
        ticket.AssignTo(agent.Id, ended);
        ticket.RecordAgentReply(session.Messages.First(m => m.Sender == ChatSender.Agent).SentAt);
        ticket.ChangeStatus(TicketStatus.Open, ended.AddMinutes(1));
        ticket.ChangeStatus(TicketStatus.Resolved, ended.AddMinutes(5));
        session.LinkTicket(ticket.Id, ticket.DisplayNumber);

        db.Add(session);
        db.Add(ticket);
        db.Add(TicketMessage.Inbound(ticket.Id, transcript, TicketChannel.Chat, null, ended));
        db.Add(CustomerInteraction.Create(
            customer.Id, InteractionType.Ticket, InteractionEvents.TicketCreated, $"{ticket.DisplayNumber} {ticket.Subject}", ticket.Id, null, ended));
    }

    private void AddOpen(Customer customer, (Guid Id, string Name)? agent, string[] script, DateTime started)
    {
        var session = NewSession(customer, started);
        if (agent is { } a)
        {
            session.Accept(a.Id, a.Name, started.AddMinutes(1));
        }

        Write(session, script, agent?.Name ?? string.Empty, started.AddSeconds(agent is null ? 0 : 60), TimeSpan.FromSeconds(75));
        db.Add(session);
    }
}
