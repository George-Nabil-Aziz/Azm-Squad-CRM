using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> attachment)
    {
        attachment.ToTable("TicketAttachments");
        attachment.HasKey(a => a.Id);
        attachment.Property(a => a.Id).ValueGeneratedNever(); // set by TicketAttachment.Create
        attachment.Property(a => a.FileName).HasMaxLength(TicketAttachment.FileNameMaxLength).IsRequired();
        attachment.Property(a => a.ContentType).HasMaxLength(TicketAttachment.ContentTypeMaxLength).IsRequired();
        attachment.Property(a => a.StorageKey).HasMaxLength(TicketAttachment.StorageKeyMaxLength).IsRequired();
        attachment.HasIndex(a => a.TicketId);

        // Tickets are never physically deleted.
        attachment.HasOne<Ticket>().WithMany().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
