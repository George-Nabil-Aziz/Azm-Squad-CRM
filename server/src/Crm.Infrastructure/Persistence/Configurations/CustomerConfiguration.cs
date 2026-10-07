using Crm.Domain.Branches;
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
        customer.HasOne<Branch>().WithMany().HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict); // CRM-62
        customer.HasIndex(c => c.BranchId);

        // Contacts belong to the customer aggregate: owned entities in their own table, always loaded with the
        // customer and hidden together with it by the soft-delete filter.
        customer.OwnsMany(c => c.Contacts, contact =>
        {
            contact.ToTable("CustomerContacts");
            contact.WithOwner().HasForeignKey(x => x.CustomerId);
            contact.HasKey(x => x.Id);
            contact.Property(x => x.Id).ValueGeneratedNever(); // set by Customer.AddContact
            contact.Property(x => x.Type).HasConversion<string>().HasMaxLength(16); // "Phone", "Email", "WhatsApp"
            contact.Property(x => x.Value).HasMaxLength(CustomerContact.ValueMaxLength).IsRequired();
            contact.HasIndex(x => new { x.Type, x.Value }); // lookup by phone / email
            contact.HasIndex(x => new { x.CustomerId, x.Type, x.Value }).IsUnique(); // no duplicate per customer
        });
        customer.Navigation(c => c.Contacts).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Soft delete: deleted customers disappear from every query; their rows (and later their tickets) stay.
        customer.HasQueryFilter(CrmDbContext.SoftDeleteFilter, c => !c.IsDeleted);
    }
}
