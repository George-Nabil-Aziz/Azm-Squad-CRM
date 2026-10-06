using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketHistoryEntryConfiguration : IEntityTypeConfiguration<TicketHistoryEntry>
{
    public void Configure(EntityTypeBuilder<TicketHistoryEntry> entry)
    {
        entry.ToTable("TicketHistory");
        entry.HasKey(x => x.Id);
        entry.Property(x => x.Id).ValueGeneratedOnAdd(); // identity: orders entries of the same time
        entry.Property(x => x.Field).HasConversion<string>().HasMaxLength(16);
        entry.Property(x => x.OldValue).HasMaxLength(TicketHistoryEntry.ValueMaxLength);
        entry.Property(x => x.NewValue).HasMaxLength(TicketHistoryEntry.ValueMaxLength);

        // Tickets and users are never physically deleted: Restrict. No navigations (the Domain knows ids only).
        entry.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        entry.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ChangedById).OnDelete(DeleteBehavior.Restrict);
        entry.HasIndex(x => new { x.TicketId, x.ChangedAt }); // one ticket's history in time order
    }
}
