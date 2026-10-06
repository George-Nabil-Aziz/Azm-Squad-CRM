using Crm.Domain.Channels;
using Crm.Domain.Customers;
using Crm.Domain.KnowledgeBase;
using Crm.Domain.Notifications;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    /// <summary>
    /// Name of the global query filter that hides soft-deleted rows (every <c>ISoftDeletable</c> entity has it).
    /// Read deleted rows on purpose with <c>IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])</c>.
    /// </summary>
    public const string SoftDeleteFilter = "SoftDelete";

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            // Rows that exist when the column is added (e.g. the seeded SuperAdmin) become active.
            user.Property(u => u.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        });

        // Domain entities: one IEntityTypeConfiguration<T> per entity in Persistence/Configurations.
        builder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Every DateTime is UTC (CLAUDE.md). The database keeps no kind, so mark values read back as UTC;
        // otherwise the API would send them without "Z" and browsers would read them as local time.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
