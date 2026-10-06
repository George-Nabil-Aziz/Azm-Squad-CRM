using Crm.Domain.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> policy)
    {
        policy.ToTable("SlaPolicies");
        policy.HasKey(p => p.Priority);
        // Stored by name ("High"), like Ticket.Priority.
        policy.Property(p => p.Priority).HasConversion<string>().HasMaxLength(10).ValueGeneratedNever();

        // CRM-19 AC 1: one seeded policy per priority (the migration inserts them; EnsureCreated in tests too).
        policy.HasData(SlaPolicy.Defaults.Select(d => new
        {
            d.Priority,
            d.ResponseMinutes,
            d.ResolutionMinutes,
            UpdatedAt = SlaPolicy.DefaultsSeededAt,
        }));
    }
}
