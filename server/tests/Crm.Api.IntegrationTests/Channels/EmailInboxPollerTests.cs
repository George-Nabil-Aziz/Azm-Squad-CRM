using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Channels;
using Crm.Application.Customers;
using Crm.Domain.Channels;
using Crm.Infrastructure.Channels.Email;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>CRM-24 (Phase 1) against the real database: polled emails are stored once and matched to customers.</summary>
public class EmailInboxPollerTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private static readonly EmailChannelOptions Options = new()
    {
        FromAddress = "support@azm.example",
        Imap = { Host = "imap.example.test", UserName = "support@azm.example" },
    };

    private sealed class FakeMailbox(params MimeMessage[] messages) : IImapMailbox
    {
        public List<MimeMessage> Unseen { get; } = [.. messages];

        public List<MimeMessage> MarkedSeen { get; } = [];

        public async Task<int> ReadUnseenAsync(
            EmailChannelOptions.ImapSettings settings, int max, Func<MimeMessage, CancellationToken, Task<bool>> handle,
            CancellationToken cancellationToken)
        {
            var count = 0;
            foreach (var message in Unseen.Take(max).ToList())
            {
                count++;
                if (await handle(message, cancellationToken))
                {
                    Unseen.Remove(message);
                    MarkedSeen.Add(message);
                }
            }

            return count;
        }
    }

    private static MimeMessage Mail(string from, string subject, string? name = null)
    {
        var mail = new MimeMessage { Subject = subject, MessageId = $"{Guid.NewGuid():N}@mail.example", Body = new TextPart("plain") { Text = "Hello" } };
        mail.From.Add(new MailboxAddress(name ?? string.Empty, from));
        return mail;
    }

    private async Task<int> PollAsync(IImapMailbox mailbox)
    {
        using var scope = factory.Services.CreateScope();
        var poller = new EmailInboxPoller(
            Options, mailbox, scope.ServiceProvider.GetRequiredService<IInboundMessageProcessor>(), factory.Time,
            NullLogger<EmailInboxPoller>.Instance);
        return await poller.PollAsync(CancellationToken.None);
    }

    private async Task<ReceivedMessage> StoredAsync(MimeMessage mail)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().ReceivedMessages.AsNoTracking()
            .SingleAsync(m => m.ExternalId == mail.MessageId);
    }

    private async Task<CustomerResponse> CreateCustomerAsync(string name, string email)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICustomerService>()
            .CreateAsync(new CustomerRequest(name, email, null), CancellationToken.None);
    }

    [Fact]
    public async Task Email_FromAKnownCustomer_IsLinkedToThatCustomer()
    {
        var email = $"known-{Guid.NewGuid():N}@example.com";
        var customer = await CreateCustomerAsync("Known Customer", email);
        var mail = Mail(email.ToUpperInvariant(), "Printer broken");
        var mailbox = new FakeMailbox(mail);

        Assert.Equal(1, await PollAsync(mailbox));

        var stored = await StoredAsync(mail);
        Assert.Equal(customer.Id, stored.CustomerId);
        Assert.Equal(ChannelKind.Email, stored.Channel);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, stored.ReceivedAt);
        Assert.Single(mailbox.MarkedSeen);
    }

    [Fact]
    public async Task Email_FromAnUnknownSender_CreatesANewCustomer()
    {
        var email = $"new-{Guid.NewGuid():N}@example.com";
        var mail = Mail(email, "Question", name: "New Sender");

        await PollAsync(new FakeMailbox(mail));

        var stored = await StoredAsync(mail);
        using var scope = factory.Services.CreateScope();
        var customer = await scope.ServiceProvider.GetRequiredService<ICustomerService>().GetAsync(stored.CustomerId!.Value, CancellationToken.None);
        Assert.Equal(("New Sender", email), (customer.Name, customer.Email));
    }

    [Fact]
    public async Task Email_WithATicketTag_StoresTheTicketNumber()
    {
        var mail = Mail($"tag-{Guid.NewGuid():N}@example.com", "Re: Printer broken [TKT-000017]");

        await PollAsync(new FakeMailbox(mail));

        Assert.Equal(17, (await StoredAsync(mail)).TicketNumber);
    }

    [Fact]
    public async Task SameEmailPolledTwice_IsStoredOnce()
    {
        var mail = Mail($"twice-{Guid.NewGuid():N}@example.com", "Hello");

        await PollAsync(new FakeMailbox(mail));
        await PollAsync(new FakeMailbox(mail)); // e.g. the "seen" flag was lost

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        Assert.Equal(1, await db.ReceivedMessages.CountAsync(m => m.ExternalId == mail.MessageId));
        Assert.Equal(1, await db.Customers.CountAsync(c => c.Email == mail.From.Mailboxes.Single().Address));
    }

    [Fact]
    public async Task Poll_WhenImapIsNotConfigured_DoesNothing()
    {
        using var scope = factory.Services.CreateScope();
        var mailbox = new FakeMailbox(Mail("x@example.com", "x"));
        var poller = new EmailInboxPoller(
            new EmailChannelOptions(), mailbox, scope.ServiceProvider.GetRequiredService<IInboundMessageProcessor>(), factory.Time,
            NullLogger<EmailInboxPoller>.Instance);

        Assert.Equal(0, await poller.PollAsync(CancellationToken.None));
        Assert.Single(mailbox.Unseen);
    }
}
