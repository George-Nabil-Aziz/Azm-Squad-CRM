using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    private const int EnumMaxLength = 16;

    public void Configure(EntityTypeBuilder<Ticket> ticket)
    {
        ticket.ToTable("Tickets");
        ticket.HasKey(t => t.Id);
        ticket.Property(t => t.Id).ValueGeneratedNever(); // set by Ticket.Create
        ticket.Property(t => t.Number).ValueGeneratedNever(); // highest + 1, set by the service
        ticket.HasIndex(t => t.Number).IsUnique(); // ticket numbers are unique, also across API instances
        ticket.Ignore(t => t.DisplayNumber);
        ticket.Property(t => t.Subject).HasMaxLength(Ticket.SubjectMaxLength).IsRequired();
        ticket.Property(t => t.Description).HasMaxLength(Ticket.DescriptionMaxLength);
        ticket.Property(t => t.Status).HasConversion<string>().HasMaxLength(EnumMaxLength);
        ticket.Property(t => t.Priority).HasConversion<string>().HasMaxLength(EnumMaxLength);
        ticket.Property(t => t.Channel).HasConversion<string>().HasMaxLength(EnumMaxLength);
        ticket.HasIndex(t => t.CreatedAt); // ticket list: newest first (CRM-14)

        // Rows a ticket points at are never physically deleted (customers are soft-deleted, categories deactivated,
        // users deactivated), so every relationship is Restrict. No navigations: the Domain knows ids only.
        ticket.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<TicketCategory>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
