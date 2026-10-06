using Crm.Domain.Ai;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketAiSummaryConfiguration : IEntityTypeConfiguration<TicketAiSummary>
{
    public void Configure(EntityTypeBuilder<TicketAiSummary> summary)
    {
        summary.ToTable("TicketAiSummaries");
        summary.HasKey(s => s.Id);
        summary.Property(s => s.Id).ValueGeneratedNever(); // set by TicketAiSummary.Create
        summary.Property(s => s.Text).HasMaxLength(TicketAiSummary.TextMaxLength).IsRequired();
        summary.Property(s => s.Language).HasMaxLength(TicketAiSummary.LanguageMaxLength).IsRequired();
        summary.HasIndex(s => s.TicketId).IsUnique(); // one summary per ticket

        summary.HasOne<Ticket>().WithMany().HasForeignKey(s => s.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
