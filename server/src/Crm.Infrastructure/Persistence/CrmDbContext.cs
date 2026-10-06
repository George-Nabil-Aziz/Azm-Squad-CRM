using Crm.Domain.Customers;
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
