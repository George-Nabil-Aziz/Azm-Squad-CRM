using Crm.Domain.Portal;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketSurveyConfiguration : IEntityTypeConfiguration<TicketSurvey>
{
    public void Configure(EntityTypeBuilder<TicketSurvey> survey)
    {
        survey.ToTable("TicketSurveys");
        survey.HasKey(s => s.Id);
        survey.Property(s => s.Id).ValueGeneratedNever(); // set by TicketSurvey.Issue
        survey.Property(s => s.Token).HasMaxLength(TicketSurvey.TokenMaxLength).IsRequired();
        survey.Property(s => s.Comment).HasMaxLength(TicketSurvey.CommentMaxLength);
        survey.HasIndex(s => s.Token).IsUnique(); // the link finds exactly one survey
        survey.HasIndex(s => s.TicketId).IsUnique(); // one survey (one rating) per ticket
        survey.HasIndex(s => s.RatedAt); // the CSAT report
        survey.HasIndex(s => s.IssuedAt);

        survey.HasOne<Ticket>().WithMany().HasForeignKey(s => s.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
