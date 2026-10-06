using Crm.Domain.QuickReplies;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class QuickReplyConfiguration : IEntityTypeConfiguration<QuickReply>
{
    public void Configure(EntityTypeBuilder<QuickReply> reply)
    {
        reply.ToTable("QuickReplies");
        reply.HasKey(r => r.Id);
        reply.Property(r => r.Id).ValueGeneratedNever();
        reply.Property(r => r.Title).HasMaxLength(QuickReply.TitleMaxLength).IsRequired();
        reply.Property(r => r.Shortcut).HasMaxLength(QuickReply.ShortcutMaxLength);
        reply.Property(r => r.Body).HasMaxLength(QuickReply.BodyMaxLength).IsRequired();
        reply.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
        reply.HasIndex(r => new { r.OwnerId, r.Title });
        reply.HasIndex(r => new { r.IsShared, r.Title });
    }
}
