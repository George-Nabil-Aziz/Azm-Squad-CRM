using Crm.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> webhook)
    {
        webhook.ToTable("Webhooks");
        webhook.HasKey(w => w.Id);
        webhook.Property(w => w.Id).ValueGeneratedNever();
        webhook.Property(w => w.Name).HasMaxLength(Webhook.NameMaxLength).IsRequired();
        webhook.Property(w => w.Url).HasMaxLength(Webhook.UrlMaxLength).IsRequired();
        webhook.Property(w => w.Events).HasMaxLength(200).IsRequired();
        webhook.Property(w => w.ProtectedSecret).HasMaxLength(1000).IsRequired();
        webhook.Ignore(w => w.EventList);
        webhook.HasIndex(w => w.IsEnabled);
    }
}

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> delivery)
    {
        delivery.ToTable("WebhookDeliveries");
        delivery.HasKey(d => d.Id);
        delivery.Property(d => d.Id).ValueGeneratedNever();
        delivery.Property(d => d.Event).HasMaxLength(50).IsRequired();
        delivery.Property(d => d.Payload).IsRequired();
        delivery.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        delivery.Property(d => d.LastError).HasMaxLength(WebhookDelivery.ErrorMaxLength);
        // Deleting a webhook keeps nothing: its deliveries go with it.
        delivery.HasOne<Webhook>().WithMany().HasForeignKey(d => d.WebhookId).OnDelete(DeleteBehavior.Cascade);
        delivery.HasIndex(d => new { d.Status, d.NextAttemptAt });
        delivery.HasIndex(d => new { d.WebhookId, d.CreatedAt });
    }
}
