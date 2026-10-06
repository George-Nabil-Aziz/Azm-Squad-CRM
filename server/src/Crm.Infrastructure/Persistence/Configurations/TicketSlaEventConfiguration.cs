using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketSlaEventConfiguration : IEntityTypeConfiguration<TicketSlaEvent>
{
    public void Configure(EntityTypeBuilder<TicketSlaEvent> slaEvent)
    {
        slaEvent.ToTable("TicketSlaEvents");
        slaEvent.HasKey(e => e.Id);
        slaEvent.Property(e => e.Id).ValueGeneratedNever();
        slaEvent.Property(e => e.Type).HasConversion<string>().HasMaxLength(32);
        // One row per ticket, kind and level: the SLA job can run twice (or on two servers) without duplicates.
        slaEvent.HasIndex(e => new { e.TicketId, e.Type, e.Level }).IsUnique();
        slaEvent.HasOne<Ticket>().WithMany().HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
