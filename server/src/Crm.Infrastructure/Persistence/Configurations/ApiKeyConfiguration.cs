using Crm.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> key)
    {
        key.ToTable("ApiKeys");
        key.HasKey(k => k.Id);
        key.Property(k => k.Id).ValueGeneratedNever();
        key.Property(k => k.Name).HasMaxLength(ApiKey.NameMaxLength).IsRequired();
        key.Property(k => k.KeyPrefix).HasMaxLength(8).IsRequired();
        key.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        key.Property(k => k.Scopes).HasMaxLength(200).IsRequired();
        key.Ignore(k => k.IsRevoked);
        key.Ignore(k => k.ScopeList);
        key.HasIndex(k => k.KeyHash).IsUnique();
    }
}
