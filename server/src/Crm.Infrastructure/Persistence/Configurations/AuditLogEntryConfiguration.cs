using Crm.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> entry)
    {
        entry.ToTable("AuditLog");
        entry.HasKey(x => x.Id);
        entry.Property(x => x.Id).ValueGeneratedOnAdd();
        entry.Property(x => x.UserEmail).HasMaxLength(AuditLogEntry.EmailMaxLength);
        entry.Property(x => x.Action).HasMaxLength(AuditLogEntry.ActionMaxLength).IsRequired();
        entry.Property(x => x.EntityType).HasMaxLength(AuditLogEntry.EntityTypeMaxLength).IsRequired();
        entry.Property(x => x.EntityId).HasMaxLength(AuditLogEntry.EntityIdMaxLength);
        entry.Property(x => x.IpAddress).HasMaxLength(AuditLogEntry.IpAddressMaxLength);

        // No foreign key to Users: an entry must outlive (and never block) any user; the email is joined when reading.
        entry.HasIndex(x => x.OccurredAt);
        entry.HasIndex(x => new { x.UserId, x.OccurredAt });
        entry.HasIndex(x => new { x.Action, x.OccurredAt });
    }
}
