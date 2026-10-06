using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Channels;
using Crm.Domain.Channels;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>CRM-23 AC 3 / AC 4 against the real database: a failed send is stored as Failed and retried later.</summary>
public class ChannelRetryTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public async Task FailedSend_IsStoredAsFailed_AndRetriedWhenDue()
    {
        var provider = new ScriptedEmailProvider { Next = ChannelSendResult.Fail("421 Service not available") };
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChannelProvider>();
            services.AddSingleton<IChannelProvider>(provider);
        }));

        OutboundMessageResponse sent;
        using (var scope = app.Services.CreateScope())
        {
            sent = await scope.ServiceProvider.GetRequiredService<IChannelSender>().SendAsync(
                new ChannelReply(ChannelKind.Email, "nour@example.com", "Re: help [TKT-000001]", "Hello", null, null),
                CancellationToken.None);
        }

        Assert.Equal("failed", sent.Status);
        Assert.Equal("421 Service not available", sent.LastError);

        provider.Next = ChannelSendResult.Ok("<id@azm.example>");
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        using (var scope = app.Services.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IChannelSender>().RetryDueAsync(CancellationToken.None) >= 1);
        }

        using (var scope = app.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().OutboundMessages
                .AsNoTracking().SingleAsync(m => m.Id == sent.Id);
            Assert.Equal(DeliveryStatus.Sent, stored.Status);
            Assert.Equal(2, stored.Attempts);
            Assert.Equal("<id@azm.example>", stored.ProviderMessageId);
            Assert.Equal(DateTimeKind.Utc, stored.UpdatedAt.Kind);
        }

        Assert.Equal(2, provider.Sent.Count(m => m.Id == sent.Id)); // AC 4: every attempt went through IChannelProvider
    }

    private sealed class ScriptedEmailProvider : IChannelProvider
    {
        public ChannelKind Channel => ChannelKind.Email;

        public bool IsConfigured => true;

        public ChannelSendResult Next { get; set; } = ChannelSendResult.Ok(null);

        public List<OutboundChannelMessage> Sent { get; } = [];

        public Task<ChannelSendResult> SendAsync(OutboundChannelMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.FromResult(Next);
        }
    }
}
