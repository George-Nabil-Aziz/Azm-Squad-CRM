using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class CustomerInteractionConfiguration : IEntityTypeConfiguration<CustomerInteraction>
{
    public void Configure(EntityTypeBuilder<CustomerInteraction> interaction)
    {
        interaction.ToTable("CustomerInteractions");
        interaction.HasKey(x => x.Id);
        interaction.Property(x => x.Id).ValueGeneratedOnAdd(); // identity: orders entries of the same time
        interaction.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        interaction.Property(x => x.Event).HasMaxLength(CustomerInteraction.EventMaxLength).IsRequired();
        interaction.Property(x => x.Details).HasMaxLength(CustomerInteraction.DetailsMaxLength);

        // No navigation: customers are soft-deleted (never physically), and the timeline service checks the customer
        // itself. Restrict keeps the history if a customer row were ever removed by hand.
        interaction.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        interaction.HasIndex(x => new { x.CustomerId, x.OccurredAt }); // one customer's timeline, newest first
    }
}
