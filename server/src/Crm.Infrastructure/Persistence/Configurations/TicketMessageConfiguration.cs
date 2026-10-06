using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    private const int EnumMaxLength = 16;

    public void Configure(EntityTypeBuilder<TicketMessage> message)
    {
        message.ToTable("TicketMessages");
        message.HasKey(m => m.Id);
        message.Property(m => m.Id).ValueGeneratedNever(); // set by the TicketMessage factories
        message.Ignore(m => m.IsInternal);
        message.Property(m => m.Body).HasMaxLength(TicketMessage.BodyMaxLength).IsRequired();
        message.Property(m => m.Direction).HasConversion<string>().HasMaxLength(EnumMaxLength);
        message.Property(m => m.Channel).HasConversion<string>().HasMaxLength(EnumMaxLength);
        message.Property(m => m.DeliveryStatus).HasConversion<string>().HasMaxLength(EnumMaxLength);
        message.Property(m => m.ExternalMessageId).HasMaxLength(TicketMessage.ExternalMessageIdMaxLength);
        message.HasIndex(m => new { m.TicketId, m.CreatedAt }); // the thread, oldest first

        // Tickets and users are never physically deleted: Restrict. No navigations (the Domain knows ids only).
        message.HasOne<Ticket>().WithMany().HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Restrict);
        message.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}
