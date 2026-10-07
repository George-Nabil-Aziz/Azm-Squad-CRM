using Crm.Domain.Customers;
using Crm.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class ErpSyncLogConfiguration : IEntityTypeConfiguration<ErpSyncLog>
{
    public void Configure(EntityTypeBuilder<ErpSyncLog> log)
    {
        log.ToTable("ErpSyncLogs");
        log.HasKey(l => l.Id);
        log.Property(l => l.Id).ValueGeneratedNever();
        log.Property(l => l.ErpCustomerId).HasMaxLength(Customer.ErpCustomerIdMaxLength).IsRequired();
        log.Property(l => l.Result).HasConversion<string>().HasMaxLength(20);
        log.Property(l => l.Error).HasMaxLength(ErpSyncLog.ErrorMaxLength);
        // Customers are only soft deleted, so the log never loses its customer.
        log.HasOne<Customer>().WithMany().HasForeignKey(l => l.CustomerId).OnDelete(DeleteBehavior.Restrict);
        log.HasIndex(l => l.CreatedAt);
    }
}
