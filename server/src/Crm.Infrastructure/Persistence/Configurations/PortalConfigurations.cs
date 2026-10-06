using Crm.Domain.Customers;
using Crm.Domain.Portal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class PortalAccountConfiguration : IEntityTypeConfiguration<PortalAccount>
{
    public void Configure(EntityTypeBuilder<PortalAccount> account)
    {
        account.ToTable("PortalAccounts");
        account.HasKey(a => a.Id);
        account.Property(a => a.Id).ValueGeneratedNever(); // set by PortalAccount.Create
        account.Property(a => a.Email).HasMaxLength(PortalLoginCode.EmailMaxLength).IsRequired();
        account.HasIndex(a => a.Email).IsUnique(); // one account per email
        account.HasIndex(a => a.CustomerId);

        // Customers are soft-deleted, never removed.
        account.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PortalLoginCodeConfiguration : IEntityTypeConfiguration<PortalLoginCode>
{
    public void Configure(EntityTypeBuilder<PortalLoginCode> code)
    {
        code.ToTable("PortalLoginCodes");
        code.HasKey(c => c.Id);
        code.Property(c => c.Id).ValueGeneratedNever(); // set by PortalLoginCode.Issue
        code.Property(c => c.Email).HasMaxLength(PortalLoginCode.EmailMaxLength).IsRequired();
        code.Property(c => c.CodeHash).HasMaxLength(PortalLoginCode.HashMaxLength).IsRequired();
        code.HasIndex(c => new { c.Email, c.CreatedAt }); // the newest code of an email
    }
}
