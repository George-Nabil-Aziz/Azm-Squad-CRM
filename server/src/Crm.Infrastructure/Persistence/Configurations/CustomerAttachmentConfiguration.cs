using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class CustomerAttachmentConfiguration : IEntityTypeConfiguration<CustomerAttachment>
{
    public void Configure(EntityTypeBuilder<CustomerAttachment> attachment)
    {
        attachment.ToTable("CustomerAttachments");
        attachment.HasKey(x => x.Id);
        attachment.Property(x => x.Id).ValueGeneratedNever(); // set by CustomerAttachment.Create
        attachment.Property(x => x.FileName).HasMaxLength(CustomerAttachment.FileNameMaxLength).IsRequired();
        attachment.Property(x => x.ContentType).HasMaxLength(CustomerAttachment.ContentTypeMaxLength).IsRequired();
        attachment.Property(x => x.StorageKey).HasMaxLength(CustomerAttachment.StorageKeyMaxLength).IsRequired();
        attachment.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        attachment.HasIndex(x => new { x.CustomerId, x.UploadedAt });
    }
}
