using Crm.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> setting)
    {
        setting.ToTable("AppSettings");
        setting.HasKey(s => s.Key);
        setting.Property(s => s.Key).HasMaxLength(AppSetting.KeyMaxLength).ValueGeneratedNever();
        setting.Property(s => s.Value).HasMaxLength(AppSetting.ValueMaxLength).IsRequired();
    }
}
