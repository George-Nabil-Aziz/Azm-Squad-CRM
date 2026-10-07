using System.Globalization;
using Crm.Application.Auth;
using Crm.Domain.Branches;
using Crm.Domain.Customers;
using Crm.Domain.Departments;
using Crm.Domain.KnowledgeBase;
using Crm.Domain.Notifications;
using Crm.Domain.Sla;
using Crm.Domain.Tasks;
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
/// Development-only demo data: staff, departments, branches, categories, knowledge base, ~60 customers and ~300 tickets over the
/// last 60 days with replies, SLA breaches and CSAT ratings, so the dashboard and every report show real numbers.
/// Runs only in the Development environment and only when <c>Seed:DemoData</c> is true; idempotent (does nothing once a customer
/// whose email ends with <see cref="DemoDataCatalog.CustomerEmailDomain"/> exists). Never sends anything: data is inserted
/// directly and no outbound message is queued.
/// </summary>
public sealed class DemoDataSeeder(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DemoDataSeeder> logger)
{
    public const string FlagKey = "Seed:DemoData";
    public const int CustomerCount = 60;
    public const int TicketCount = 300;

    /// <summary>Startup entry point: seeds when enabled; a failure is logged and never stops the API from starting.</summary>
    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var seeder = ActivatorUtilities.CreateInstance<DemoDataSeeder>(services);
        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<DemoDataSeeder>>().LogError(exception, "Seeding the development demo data failed.");
        }

        await DemoTaskSeeder.RunAsync(services, cancellationToken); // separate step: also tops up a database that already has demo data
        await DemoChatSeeder.RunAsync(services, cancellationToken); // separate step with its own marker (chats of demo visitors)
    }

    /// <summary>Returns true when data was added.</summary>
    public async Task<bool> SeedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>(FlagKey))
        {
            return false;
        }

        if (await db.Customers.IgnoreQueryFilters()
                .AnyAsync(c => c.Email != null && c.Email.EndsWith(DemoDataCatalog.CustomerEmailDomain), cancellationToken))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rnd = new Random(20260607);
        var sink = new List<object>();

        var staff = await EnsureStaffAsync();
        var departments = await EnsureByNameAsync(
            DemoDataCatalog.DepartmentNames, Department.NormalizeName, n => db.Departments.FirstOrDefaultAsync(d => d.NormalizedName == n, cancellationToken),
            name => Department.Create(name, now.AddDays(-120)), d => d.Id, sink);
        var branches = await EnsureByNameAsync(
            DemoDataCatalog.BranchNames, Branch.NormalizeName, n => db.Branches.FirstOrDefaultAsync(b => b.NormalizedName == n, cancellationToken),
            name => Branch.Create(name, now.AddDays(-120)), b => b.Id, sink);

        foreach (var member in staff)
        {
            foreach (var index in member.Departments)
            {
                if (!await db.UserDepartments.AnyAsync(m => m.UserId == member.UserId && m.DepartmentId == departments[index], cancellationToken))
                {
                    sink.Add(new UserDepartment(member.UserId, departments[index]));
                }
            }
        }

        var categories = await db.TicketCategories.Where(c => c.IsActive).OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);
        if (categories.Count == 0)
        {
            categories = [.. DemoDataCatalog.CategoryNames.Select(n => TicketCategory.Create(n, now.AddDays(-120)))];
            sink.AddRange(categories);
        }

        AddKnowledgeBase(now, rnd, sink);

        var agents = staff.Where(s => s.Role == Roles.Agent).ToList();
        var supervisor = staff.FirstOrDefault(s => s.Role == Roles.Supervisor);
        var customers = BuildCustomers(now, rnd, branches, agents, sink);

        var policies = await LoadPoliciesAsync(now, cancellationToken);
        var byDepartment = Enumerable.Range(0, departments.Count).ToDictionary(
            i => i,
            i => (IReadOnlyList<DemoAgent>)[.. agents.Where(a => a.Departments.Contains(i)).Select(a => a.Profile)]);
        var factory = new DemoTicketFactory(rnd, now, policies, sink)
        {
            CategoryIds = [.. categories.Select(c => c.Id)],
            DepartmentIds = [.. departments.Select(d => (Guid?)d)],
            AgentsByDepartment = byDepartment,
            SupervisorId = supervisor?.UserId,
        };

        var number = ((await db.Tickets.IgnoreQueryFilters().MaxAsync(t => (int?)t.Number, cancellationToken)) ?? 0) + 1;
        var dayWeights = Enumerable.Range(0, 60).Select(d => DayWeight(now, d)).ToArray();
        var customerWeights = customers.Select((_, i) => 1 / Math.Pow(i + 1, 0.8)).ToArray();
        for (var i = 0; i < TicketCount; i++)
        {
            var special = i < 2; // two unanswered High tickets whose response is overdue since earlier today
            var created = special ? now.AddMinutes(-(150 - (i * 15))) : RandomCreatedAt(now, rnd, dayWeights);
            factory.Build(number++, created, customers[WeightedIndex(rnd, customerWeights)], special);
        }

        AddAgentExtras(now, rnd, staff, factory.Tickets, sink);
        foreach (var entity in sink)
        {
            db.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Seeded development demo data: {Customers} customers, {Tickets} tickets, {Ratings} CSAT ratings. Turn off with {Flag}=false.",
            customers.Count, factory.Tickets.Count, factory.RatedCount, FlagKey);
        return true;
    }

    private sealed record StaffMember(Guid UserId, string Role, int[] Departments, DemoAgent Profile, string Email);

    private async Task<List<StaffMember>> EnsureStaffAsync()
    {
        var result = new List<StaffMember>();
        var password = configuration["Seed:SuperAdminPassword"];
        var defs = new List<DemoDataCatalog.StaffDef>(DemoDataCatalog.Staff) { DemoDataCatalog.Supervisor };
        foreach (var def in defs)
        {
            var user = await userManager.FindByEmailAsync(def.Email);
            var created = false;
            if (user is null)
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    logger.LogWarning("Seed:SuperAdminPassword is not configured; demo user {Email} was not created.", def.Email);
                    continue;
                }

                user = new ApplicationUser { UserName = def.Email, Email = def.Email, EmailConfirmed = true, FullName = def.NameEn };
                Check(await userManager.CreateAsync(user, password), $"create {def.Email}");
                Check(await userManager.AddToRoleAsync(user, def.Role), $"add the {def.Role} role to {def.Email}");
                created = true;
            }

            // An account that already existed (not made by this seeder) is left alone, except supervisor@crm.com which the demo logins create.
            if (created || def.Role == Roles.Supervisor)
            {
                result.Add(Member(user, def));
            }
        }

        if (await userManager.FindByEmailAsync(DemoDataCatalog.DemoAgentEmail) is { } demoAgent)
        {
            var def = new DemoDataCatalog.StaffDef(DemoDataCatalog.DemoAgentEmail, demoAgent.FullName, demoAgent.FullName, Roles.Agent, [0, 1, 2], 1.5, 1.0, 0.0, 0.1);
            result.Add(Member(demoAgent, def));
        }

        return result;
    }

    private static StaffMember Member(ApplicationUser user, DemoDataCatalog.StaffDef def) =>
        new(user.Id, def.Role, def.Departments, new DemoAgent(user.Id, string.IsNullOrWhiteSpace(user.FullName) ? def.NameEn : user.FullName, def.Weight, def.Speed, def.ExtraBreach, def.Quality), def.Email);

    private static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Demo data: failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    private static async Task<List<Guid>> EnsureByNameAsync<T>(
        string[] names, Func<string, string> normalize, Func<string, Task<T?>> find, Func<string, T> create, Func<T, Guid> id, List<object> sink)
        where T : class
    {
        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var existing = await find(normalize(name));
            if (existing is null)
            {
                existing = create(name);
                sink.Add(existing);
            }

            ids.Add(id(existing));
        }

        return ids;
    }

    private async Task<Dictionary<TicketPriority, SlaPolicy>> LoadPoliciesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var policies = await db.SlaPolicies.AsNoTracking().ToDictionaryAsync(p => p.Priority, cancellationToken);
        foreach (var (priority, response, resolution) in SlaPolicy.Defaults)
        {
            if (!policies.ContainsKey(priority))
            {
                policies[priority] = SlaPolicy.Create(priority, response, resolution, now); // in memory only, never saved
            }
        }

        return policies;
    }

    private static double DayWeight(DateTime now, int daysAgo)
    {
        var day = now.Date.AddDays(-daysAgo).DayOfWeek;
        var recency = 1 + (5.0 * Math.Exp(-daysAgo / 6.0)); // the latest days are the busiest
        var weekend = day is DayOfWeek.Friday or DayOfWeek.Saturday ? 0.35 : 1.0;
        return recency * weekend;
    }

    private static DateTime RandomCreatedAt(DateTime now, Random rnd, double[] dayWeights)
    {
        var day = WeightedIndex(rnd, dayWeights);
        var localHour = rnd.NextDouble() < 0.82 ? rnd.Next(8, 17) : rnd.Next(0, 24); // mostly working hours (UTC+3)
        var created = now.Date.AddDays(-day).AddHours(localHour - 3).AddMinutes(rnd.Next(60)).AddSeconds(rnd.Next(60));
        return created > now.AddMinutes(-5) ? now.AddMinutes(-rnd.Next(5, 180)) : created;
    }

    private static int WeightedIndex(Random rnd, double[] weights)
    {
        var roll = rnd.NextDouble() * weights.Sum();
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0)
            {
                return i;
            }
        }

        return weights.Length - 1;
    }

    private List<DemoCustomer> BuildCustomers(DateTime now, Random rnd, List<Guid> branches, List<StaffMember> agents, List<object> sink)
    {
        var customers = new List<DemoCustomer>();
        var companyIndex = 0;
        for (var i = 0; i < CustomerCount; i++)
        {
            var saudi = rnd.NextDouble() < 0.75;
            var city = saudi ? DemoDataCatalog.SaudiCities[rnd.Next(DemoDataCatalog.SaudiCities.Length)] : DemoDataCatalog.EgyptCities[rnd.Next(DemoDataCatalog.EgyptCities.Length)];
            var arabic = rnd.NextDouble() < 0.55;
            var isCompany = i % 4 == 3 && companyIndex < DemoDataCatalog.Companies.Length;
            string name, slug;
            (string Ar, string En) company = default;
            if (isCompany)
            {
                company = DemoDataCatalog.Companies[companyIndex++];
                name = arabic ? company.Ar : company.En;
                slug = new string([.. company.En.ToLowerInvariant().Where(char.IsAsciiLetter)]);
            }
            else
            {
                var first = DemoDataCatalog.FirstNames[rnd.Next(DemoDataCatalog.FirstNames.Length)];
                var last = DemoDataCatalog.LastNames[rnd.Next(DemoDataCatalog.LastNames.Length)];
                name = arabic ? $"{first.Ar} {last.Ar}" : $"{first.En} {last.En}";
                slug = $"{first.En}.{last.En}".ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
            }

            var phone = saudi
                ? $"+9665{rnd.Next(0, 100_000_000):D8}"
                : $"+201{new[] { 0, 1, 2, 5 }[rnd.Next(4)]}{rnd.Next(0, 100_000_000):D8}";
            var createdAt = now.AddDays(-rnd.Next(61, 150)).AddMinutes(rnd.Next(1440));
            var customer = Customer.Create(name, $"{slug}{i}{DemoDataCatalog.CustomerEmailDomain}", phone, createdAt);
            customer.ChangeBranch(branches[saudi ? 0 : 1], createdAt);
            if (rnd.NextDouble() < 0.4)
            {
                customer.AddContact(ContactType.WhatsApp, phone, true, createdAt);
            }

            sink.Add(customer);
            sink.Add(CustomerInteraction.Create(customer.Id, InteractionType.Customer, InteractionEvents.CustomerCreated, name, null, null, createdAt));
            if (rnd.NextDouble() < 0.45)
            {
                var author = agents.Count == 0 ? null : (Guid?)agents[rnd.Next(agents.Count)].UserId;
                var notes = arabic ? DemoDataCatalog.CustomerNotesAr : DemoDataCatalog.CustomerNotesEn;
                var companyName = isCompany ? (arabic ? company.Ar : company.En) : (arabic ? "شركة خاصة" : "a private company");
                var text = notes[rnd.Next(notes.Length)]
                    .Replace("{city}", arabic ? city.Ar : city.En, StringComparison.Ordinal)
                    .Replace("{company}", companyName, StringComparison.Ordinal);
                var noteAt = createdAt.AddDays(rnd.Next(1, 20));
                var note = CustomerNote.Create(customer.Id, text, author, noteAt);
                sink.Add(note);
                sink.Add(CustomerInteraction.Create(customer.Id, InteractionType.Note, InteractionEvents.NoteAdded, null, note.Id, author, noteAt));
            }

            customers.Add(new DemoCustomer(customer, arabic));
        }

        return customers;
    }

    private static void AddKnowledgeBase(DateTime now, Random rnd, List<object> sink)
    {
        var categories = DemoDataCatalog.KbCategories
            .Select(c => KbCategory.Create(c.En, c.Ar, now.AddDays(-90))).ToList();
        sink.AddRange(categories);
        foreach (var article in DemoDataCatalog.KbArticles)
        {
            var created = now.AddDays(-rnd.Next(20, 80));
            var entity = KbArticle.Create(categories[article.Category].Id, article.TitleEn, article.BodyEn, article.TitleAr, article.BodyAr, created);
            entity.Publish(created.AddHours(2));
            for (var i = rnd.Next(2, 30); i > 0; i--)
            {
                entity.RecordFeedback(rnd.NextDouble() < 0.85);
            }

            sink.Add(entity);
        }

        var order = 0;
        foreach (var faq in DemoDataCatalog.KbFaqs)
        {
            sink.Add(KbFaq.Create(faq.QEn, faq.AEn, faq.QAr, faq.AAr, order++, true, now.AddDays(-60)));
        }
    }

    private static void AddAgentExtras(DateTime now, Random rnd, List<StaffMember> staff, List<Ticket> tickets, List<object> sink)
    {
        var demo = staff.FirstOrDefault(s => s.Email == DemoDataCatalog.DemoAgentEmail)
                   ?? staff.FirstOrDefault(s => s.Role == Roles.Agent);
        if (demo is null)
        {
            return;
        }

        var open = tickets.Where(t => t.Status is TicketStatus.Open or TicketStatus.New or TicketStatus.Pending).OrderByDescending(t => t.CreatedAt).ToList();
        var mine = open.Where(t => t.AssigneeId == demo.UserId).ToList();
        var pool = mine.Count >= 4 ? mine : open;
        var types = new[] { NotificationType.Assignment, NotificationType.Assignment, NotificationType.SlaWarning, NotificationType.Assignment, NotificationType.TaskReminder, NotificationType.SlaWarning };
        for (var i = 0; i < types.Length && i < pool.Count; i++)
        {
            var ticket = pool[i];
            var at = now.AddMinutes(-(20 + (i * 55)));
            var text = types[i] switch
            {
                NotificationType.Assignment => $"{ticket.DisplayNumber} was assigned to you.",
                NotificationType.SlaWarning => $"{ticket.DisplayNumber} is close to its SLA due time.",
                _ => $"Reminder: follow up on {ticket.DisplayNumber}.",
            };
            var notification = Notification.ForUser(demo.UserId, ticket.Id, types[i], 0, $"demo:{types[i]}:{ticket.Id}", text, at);
            if (i == 5)
            {
                notification.MarkRead(at.AddMinutes(10));
            }

            sink.Add(notification);
        }

        for (var i = 0; i < DemoDataCatalog.TaskTitlesEn.Length; i++)
        {
            var title = i % 2 == 0 ? DemoDataCatalog.TaskTitlesEn[i] : DemoDataCatalog.TaskTitlesAr[i];
            var linked = pool.Count > i ? pool[i].Id : (Guid?)null;
            var task = WorkTask.Create(demo.UserId, title, null, now.AddHours(4 + (i * 20) + rnd.Next(0, 6)), linked, now);
            sink.Add(task);
        }
    }
}
