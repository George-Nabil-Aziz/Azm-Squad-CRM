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

public sealed class TicketAiClassificationConfiguration : IEntityTypeConfiguration<TicketAiClassification>
{
    public void Configure(EntityTypeBuilder<TicketAiClassification> classification)
    {
        classification.ToTable("TicketAiClassifications");
        classification.HasKey(c => c.Id);
        classification.Property(c => c.Id).ValueGeneratedNever(); // set by TicketAiClassification.Create
        classification.Property(c => c.SuggestedPriority).HasConversion<string>().HasMaxLength(16);
        classification.Property(c => c.PriorityOverriddenTo).HasConversion<string>().HasMaxLength(16);
        classification.HasIndex(c => c.TicketId).IsUnique(); // one suggestion per ticket

        classification.HasOne<Ticket>().WithMany().HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
