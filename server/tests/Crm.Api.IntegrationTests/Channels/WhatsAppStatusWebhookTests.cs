using System.Net;
using System.Text;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Domain.Channels;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>CRM-25 AC 3: delivery statuses from the WhatsApp webhook update the outbound message.</summary>
public class WhatsAppStatusWebhookTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private static byte[] StatusPayload(string id, string status, string? error = null)
    {
        var errors = error is null ? string.Empty : $",\"errors\":[{{\"code\":131026,\"title\":\"{error}\"}}]";
        return Encoding.UTF8.GetBytes(
            "{\"object\":\"whatsapp_business_account\",\"entry\":[{\"id\":\"WABA\",\"changes\":[{\"field\":\"messages\",\"value\":{"
            + "\"messaging_product\":\"whatsapp\",\"metadata\":{\"phone_number_id\":\"PHONE_ID\"},"
            + $"\"statuses\":[{{\"id\":\"{id}\",\"status\":\"{status}\",\"timestamp\":\"1791273600\",\"recipient_id\":\"966501234567\"{errors}}}]}}}}]}}]}}");
    }

    private async Task<string> StoreSentMessageAsync()
    {
        var wamid = $"wamid.{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var message = OutboundMessage.Create(ChannelKind.WhatsApp, "+966501234567", null, "Hi", "hello_world", null, DateTime.UtcNow);
        message.MarkSent(wamid, DateTime.UtcNow);
        db.OutboundMessages.Add(message);
        await db.SaveChangesAsync();
        return wamid;
    }

    private async Task<OutboundMessage> LoadAsync(string wamid)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().OutboundMessages.AsNoTracking()
            .SingleAsync(m => m.ProviderMessageId == wamid);
    }

    [Fact]
    public async Task Delivered_ThenRead_UpdatesTheStatus()
    {
        var wamid = await StoreSentMessageAsync();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await WhatsAppWebhookTests.PostSignedAsync(client, StatusPayload(wamid, "delivered"))).StatusCode);
        Assert.Equal(DeliveryStatus.Delivered, (await LoadAsync(wamid)).Status);
        await WhatsAppWebhookTests.PostSignedAsync(client, StatusPayload(wamid, "read"));
        await WhatsAppWebhookTests.PostSignedAsync(client, StatusPayload(wamid, "delivered"));

        Assert.Equal(DeliveryStatus.Read, (await LoadAsync(wamid)).Status);
    }

    [Fact]
    public async Task Failed_StoresTheError()
    {
        var wamid = await StoreSentMessageAsync();

        await WhatsAppWebhookTests.PostSignedAsync(factory.CreateClient(), StatusPayload(wamid, "failed", "Re-engagement message"));

        var stored = await LoadAsync(wamid);
        Assert.Equal((DeliveryStatus.Failed, "Re-engagement message"), (stored.Status, stored.LastError));
    }

    [Fact]
    public async Task UnknownMessageId_StillAnswers200()
    {
        var response = await WhatsAppWebhookTests.PostSignedAsync(factory.CreateClient(), StatusPayload("wamid.unknown", "read"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
