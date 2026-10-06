using Crm.Domain.Channels;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class ReceivedMessageConfiguration : IEntityTypeConfiguration<ReceivedMessage>
{
    public void Configure(EntityTypeBuilder<ReceivedMessage> message)
    {
        message.ToTable("ReceivedMessages");
        message.HasKey(x => x.Id);
        message.Property(x => x.Id).ValueGeneratedNever();
        message.Property(x => x.Channel).HasConversion<string>().HasMaxLength(16);
        message.Property(x => x.ExternalId).HasMaxLength(ReceivedMessage.ExternalIdMaxLength).IsRequired();
        message.Property(x => x.From).HasMaxLength(ReceivedMessage.FromMaxLength).IsRequired();
        message.Property(x => x.FromName).HasMaxLength(ReceivedMessage.FromNameMaxLength);
        message.Property(x => x.Subject).HasMaxLength(ReceivedMessage.SubjectMaxLength);
        message.Property(x => x.Body).HasMaxLength(ReceivedMessage.BodyMaxLength).IsRequired();

        // The same email / WhatsApp message is stored once (CRM-24 AC 4): the race guard behind the ExistsAsync check.
        message.HasIndex(x => new { x.Channel, x.ExternalId }).IsUnique();
        message.HasIndex(x => new { x.Channel, x.From, x.ReceivedAt }); // last message of a sender (WhatsApp window)

        // No navigation; Restrict keeps the message if a customer row were ever removed by hand.
        message.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        message.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
