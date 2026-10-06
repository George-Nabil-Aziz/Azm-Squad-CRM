using Crm.Domain.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> message)
    {
        message.ToTable("OutboundMessages");
        message.HasKey(x => x.Id);
        message.Property(x => x.Id).ValueGeneratedNever();
        message.Property(x => x.Channel).HasConversion<string>().HasMaxLength(16);
        message.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        message.Property(x => x.Recipient).HasMaxLength(OutboundMessage.RecipientMaxLength).IsRequired();
        message.Property(x => x.Subject).HasMaxLength(OutboundMessage.SubjectMaxLength);
        message.Property(x => x.Body).HasMaxLength(OutboundMessage.BodyMaxLength).IsRequired();
        message.Property(x => x.TemplateName).HasMaxLength(OutboundMessage.TemplateNameMaxLength);
        message.Property(x => x.LastError).HasMaxLength(OutboundMessage.ErrorMaxLength);
        message.Property(x => x.ProviderMessageId).HasMaxLength(OutboundMessage.ProviderMessageIdMaxLength);

        message.HasIndex(x => new { x.Status, x.NextAttemptAt }); // retry job: failed and due
        message.HasIndex(x => x.ProviderMessageId);               // delivery-status webhooks
        message.HasIndex(x => x.SourceId);                        // status of a ticket reply
    }
}
