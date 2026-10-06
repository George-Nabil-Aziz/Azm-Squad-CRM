using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
{
    public void Configure(EntityTypeBuilder<CustomerNote> note)
    {
        note.ToTable("CustomerNotes");
        note.HasKey(x => x.Id);
        note.Property(x => x.Id).ValueGeneratedNever(); // set by CustomerNote.Create
        note.Property(x => x.Text).HasMaxLength(CustomerNote.TextMaxLength).IsRequired();
        note.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        note.HasIndex(x => new { x.CustomerId, x.CreatedAt });
    }
}
