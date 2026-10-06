using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> customer)
    {
        customer.ToTable("Customers");
        customer.HasKey(c => c.Id);
        customer.Property(c => c.Id).ValueGeneratedNever(); // set by Customer.Create
        customer.Property(c => c.Name).HasMaxLength(Customer.NameMaxLength).IsRequired();
        customer.Property(c => c.Email).HasMaxLength(Customer.EmailMaxLength);
        customer.Property(c => c.Phone).HasMaxLength(Customer.PhoneMaxLength);
        customer.HasIndex(c => c.Name); // list order

        // Soft delete: deleted customers disappear from every query; their rows (and later their tickets) stay.
        customer.HasQueryFilter(CrmDbContext.SoftDeleteFilter, c => !c.IsDeleted);
    }
}
