using Crm.Domain.Chat;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> session)
    {
        session.ToTable("ChatSessions");
        session.HasKey(x => x.Id);
        session.Property(x => x.Id).ValueGeneratedNever();
        session.Property(x => x.VisitorName).HasMaxLength(ChatSession.NameMaxLength).IsRequired();
        session.Property(x => x.VisitorEmail).HasMaxLength(ChatSession.EmailMaxLength).IsRequired();
        session.Property(x => x.VisitorTokenHash).HasMaxLength(ChatSession.TokenHashLength).IsRequired();
        session.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        session.Property(x => x.AgentName).HasMaxLength(ChatSession.NameMaxLength);
        session.Property(x => x.TicketNumber).HasMaxLength(32);

        // The AgentId is the staff user's id; no foreign key so a deactivated / removed user never blocks old chats.
        session.HasIndex(x => new { x.Status, x.StartedAt });
        session.HasIndex(x => new { x.AgentId, x.Status });
        session.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);

        session.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        session.Navigation(x => x.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> message)
    {
        message.ToTable("ChatMessages");
        message.HasKey(x => x.Id);
        message.Property(x => x.Id).ValueGeneratedNever();
        message.Property(x => x.Sender).HasConversion<string>().HasMaxLength(16);
        message.Property(x => x.SenderName).HasMaxLength(ChatMessage.SenderNameMaxLength).IsRequired();
        message.Property(x => x.Body).HasMaxLength(ChatMessage.BodyMaxLength).IsRequired();
        message.HasIndex(x => new { x.SessionId, x.SentAt });
    }
}
