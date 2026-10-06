using Crm.Domain.Departments;
using Crm.Domain.Sla;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> department)
    {
        department.ToTable("Departments");
        department.HasKey(d => d.Id);
        department.Property(d => d.Id).ValueGeneratedNever(); // set by Department.Create
        department.Property(d => d.Name).HasMaxLength(Department.NameMaxLength).IsRequired();
        department.Property(d => d.NormalizedName).HasMaxLength(Department.NameMaxLength).IsRequired();
        department.HasIndex(d => d.NormalizedName).IsUnique();
    }
}

public sealed class UserDepartmentConfiguration : IEntityTypeConfiguration<UserDepartment>
{
    public void Configure(EntityTypeBuilder<UserDepartment> membership)
    {
        membership.ToTable("UserDepartments");
        membership.HasKey(m => new { m.UserId, m.DepartmentId });
        membership.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        membership.HasOne<Department>().WithMany().HasForeignKey(m => m.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        membership.HasIndex(m => m.DepartmentId);
    }
}

public sealed class DepartmentSlaPolicyConfiguration : IEntityTypeConfiguration<DepartmentSlaPolicy>
{
    public void Configure(EntityTypeBuilder<DepartmentSlaPolicy> policy)
    {
        policy.ToTable("DepartmentSlaPolicies");
        policy.HasKey(p => new { p.DepartmentId, p.Priority });
        policy.Property(p => p.Priority).HasConversion<string>().HasMaxLength(10);
        policy.HasOne<Department>().WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
