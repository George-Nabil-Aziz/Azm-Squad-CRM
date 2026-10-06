using Crm.Application.Common.Security;
using Crm.Domain.Audit;
using Crm.Domain.Channels;
using Crm.Domain.Customers;
using Crm.Domain.Departments;
using Crm.Domain.KnowledgeBase;
using Crm.Domain.Notifications;
using Crm.Domain.Portal;
using Crm.Domain.Settings;
using Crm.Domain.QuickReplies;
using Crm.Domain.Sla;
using Crm.Domain.Tasks;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public class CrmDbContext(DbContextOptions<CrmDbContext> options, IDataScope? dataScope = null)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    /// <summary>
    /// Name of the global query filter that hides soft-deleted rows (every <c>ISoftDeletable</c> entity has it).
    /// Read deleted rows on purpose with <c>IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])</c>.
    /// </summary>
    public const string SoftDeleteFilter = "SoftDelete";

    /// <summary>Name of the global query filter that hides tickets of other departments from a department-restricted agent (CRM-61).</summary>
    public const string DepartmentFilter = "Department";

    // The data scope of the current request (null / unrestricted for jobs, channels and the portal). Read by the query filters
    // below on every query, so EF Core takes the values of this context instance.
    internal bool ScopeRestrictsDepartments => dataScope is { RestrictDepartments: true };

    internal Guid[] ScopeDepartmentIds => dataScope is null ? [] : [.. dataScope.DepartmentIds];

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();

    public DbSet<DepartmentSlaPolicy> DepartmentSlaPolicies => Set<DepartmentSlaPolicy>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();

    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();

    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();

    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();

    public DbSet<ReceivedMessage> ReceivedMessages => Set<ReceivedMessage>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    public DbSet<TicketHistoryEntry> TicketHistory => Set<TicketHistoryEntry>();
    public DbSet<TicketSlaEvent> TicketSlaEvents => Set<TicketSlaEvent>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<KbCategory> KbCategories => Set<KbCategory>();

    public DbSet<KbArticle> KbArticles => Set<KbArticle>();

    public DbSet<KbFaq> KbFaqs => Set<KbFaq>();

    public DbSet<TicketArticleLink> TicketArticleLinks => Set<TicketArticleLink>();

    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    public DbSet<TicketSurvey> TicketSurveys => Set<TicketSurvey>();

    public DbSet<PortalAccount> PortalAccounts => Set<PortalAccount>();

    public DbSet<PortalLoginCode> PortalLoginCodes => Set<PortalLoginCode>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<WorkTask> Tasks => Set<WorkTask>();

    public DbSet<QuickReply> QuickReplies => Set<QuickReply>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            // Rows that exist when the column is added (e.g. the seeded SuperAdmin) become active.
            user.Property(u => u.IsActive).HasDefaultValue(true).ValueGeneratedNever();
            user.Property(u => u.IsOnDuty).HasDefaultValue(true).ValueGeneratedNever();
        });

        // Domain entities: one IEntityTypeConfiguration<T> per entity in Persistence/Configurations.
        builder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);

        // CRM-61: a department-restricted agent sees tickets of their departments and tickets without a department.
        builder.Entity<Ticket>().HasQueryFilter(DepartmentFilter,
            t => !ScopeRestrictsDepartments || t.DepartmentId == null || ScopeDepartmentIds.Contains(t.DepartmentId.Value));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Every DateTime is UTC (CLAUDE.md). The database keeps no kind, so mark values read back as UTC;
        // otherwise the API would send them without "Z" and browsers would read them as local time.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
