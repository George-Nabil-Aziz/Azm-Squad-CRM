using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> category)
    {
        category.ToTable("TicketCategories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Id).ValueGeneratedNever(); // set by TicketCategory.Create
        category.Property(c => c.Name).HasMaxLength(TicketCategory.NameMaxLength).IsRequired();
        category.Property(c => c.NormalizedName).HasMaxLength(TicketCategory.NameMaxLength).IsRequired();
        // One category per name, ignoring case and spaces (same behaviour on SQL Server and SQLite).
        category.HasIndex(c => c.NormalizedName).IsUnique();
    }
}
