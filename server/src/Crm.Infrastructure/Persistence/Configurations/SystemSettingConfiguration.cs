using Crm.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> setting)
    {
        setting.ToTable("SystemSettings");
        setting.HasKey(s => s.Key);
        setting.Property(s => s.Key).HasMaxLength(SystemSetting.KeyMaxLength).ValueGeneratedNever();
        // Secrets are stored encrypted (Data Protection payloads are long text), so the value has no small limit.
    }
}
