using Crm.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> branch)
    {
        branch.ToTable("Branches");
        branch.HasKey(b => b.Id);
        branch.Property(b => b.Id).ValueGeneratedNever(); // set by Branch.Create
        branch.Property(b => b.Name).HasMaxLength(Branch.NameMaxLength).IsRequired();
        branch.Property(b => b.NormalizedName).HasMaxLength(Branch.NameMaxLength).IsRequired();
        branch.HasIndex(b => b.NormalizedName).IsUnique();
    }
}
