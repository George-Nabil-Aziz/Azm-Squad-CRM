using Crm.Domain.Notifications;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> notification)
    {
        notification.ToTable("Notifications");
        notification.HasKey(n => n.Id);
        notification.Property(n => n.Id).ValueGeneratedNever();
        notification.Property(n => n.Type).HasConversion<string>().HasMaxLength(32);
        notification.Property(n => n.Text).HasMaxLength(Notification.TextMaxLength);
        notification.Property(n => n.DedupKey).HasMaxLength(Notification.DedupKeyMaxLength).IsRequired();
        notification.Ignore(n => n.IsRead);
        notification.HasIndex(n => new { n.RecipientUserId, n.ReadAt });

        // CRM-28 AC 4: one notification per user and event. Rows stored by CRM-22 for a role have no user and are left out.
        notification.HasIndex(n => new { n.RecipientUserId, n.DedupKey }).IsUnique().HasFilter("[RecipientUserId] IS NOT NULL");

        notification.HasOne<Ticket>().WithMany().HasForeignKey(n => n.TicketId).OnDelete(DeleteBehavior.Restrict);
        notification.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
