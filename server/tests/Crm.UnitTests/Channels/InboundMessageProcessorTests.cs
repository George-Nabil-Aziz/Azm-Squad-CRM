using Crm.Application.Channels;
using Crm.Domain.Channels;

namespace Crm.UnitTests.Channels;

public class InboundMessageProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeReceivedMessageRepository _received = new();
    private readonly FakeCustomerService _customers = new();

    private InboundMessageProcessor CreateProcessor() => new(_received, _customers, new ChannelClock(Now));

    private static InboundChannelMessage Email(string from, string? subject = "Printer broken", string id = "<m1@mail.example>", string? name = "Nour") =>
        new(ChannelKind.Email, id, from, name, subject, "It does not print.", Now.UtcDateTime.AddMinutes(-2));

    [Fact]
    public async Task KnownSender_IsLinkedToThatCustomer()
    {
        var customer = _customers.AddCustomer("Nour Trading", email: "nour@example.com");

        var result = await CreateProcessor().ProcessAsync(Email("nour@example.com"), CancellationToken.None);

        Assert.False(result.Duplicate);
        Assert.False(result.NewCustomer);
        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Empty(_customers.Created);
        var stored = Assert.Single(_received.Messages);
        Assert.Equal((customer.Id, "Printer broken", "It does not print."), (stored.CustomerId, stored.Subject, stored.Body));
        Assert.Equal(result.ReceivedMessageId, stored.Id);
    }

    [Fact]
    public async Task UnknownSender_CreatesANewCustomer_WithNameAndEmail()
    {
        var result = await CreateProcessor().ProcessAsync(Email("new.person@example.com", name: "New Person"), CancellationToken.None);

        var request = Assert.Single(_customers.Created);
        Assert.Equal(("New Person", "new.person@example.com", (string?)null), (request.Name, request.Email, request.Phone));
        Assert.True(result.NewCustomer);
        Assert.Equal(_customers.Customers.Single().Id, result.CustomerId);
        Assert.Equal(result.CustomerId, Assert.Single(_received.Messages).CustomerId);
    }

    [Fact]
    public async Task UnknownSender_WithoutName_IsNamedByTheAddress()
    {
        await CreateProcessor().ProcessAsync(Email("someone@example.com", name: null), CancellationToken.None);

        Assert.Equal("someone@example.com", Assert.Single(_customers.Created).Name);
    }

    [Fact]
    public async Task SubjectWithTicketTag_StoresTheTicketNumber()
    {
        _customers.AddCustomer("Nour Trading", email: "nour@example.com");

        var result = await CreateProcessor().ProcessAsync(
            Email("nour@example.com", subject: "Re: Printer broken [TKT-000042]"), CancellationToken.None);

        Assert.Equal(42, result.TicketNumber);
        Assert.Equal(42, Assert.Single(_received.Messages).TicketNumber);
    }

    [Fact]
    public async Task SameMessageIdTwice_IsIgnored()
    {
        var processor = CreateProcessor();
        await processor.ProcessAsync(Email("someone@example.com"), CancellationToken.None);

        var second = await processor.ProcessAsync(Email("someone@example.com"), CancellationToken.None);

        Assert.True(second.Duplicate);
        Assert.Single(_received.Messages);
        Assert.Single(_customers.Created);
    }

    [Fact]
    public async Task ConcurrentDuplicate_DetectedOnSave_IsIgnored()
    {
        _customers.AddCustomer("Nour Trading", email: "nour@example.com");
        _received.NextSaveIsDuplicate = true;

        var result = await CreateProcessor().ProcessAsync(Email("nour@example.com"), CancellationToken.None);

        Assert.True(result.Duplicate);
        Assert.Empty(_received.Messages);
    }

    [Fact]
    public async Task SeveralMatches_TakesTheFirst()
    {
        _customers.AddCustomer("Zeta Co", email: "shared@example.com");
        var first = _customers.AddCustomer("Alpha Co", email: "shared@example.com");

        var result = await CreateProcessor().ProcessAsync(Email("shared@example.com"), CancellationToken.None);

        Assert.Equal(first.Id, result.CustomerId);
    }

    private static InboundChannelMessage WhatsApp(string from, string? name = "Nour", string id = "wamid.1") =>
        new(ChannelKind.WhatsApp, id, from, name, null, "Where is my order?", Now.UtcDateTime.AddMinutes(-1));

    [Fact]
    public async Task KnownWhatsAppNumber_IsLinkedToThatCustomer()
    {
        var customer = _customers.AddCustomer("Nour Trading", whatsApp: "+966501234567");

        var result = await CreateProcessor().ProcessAsync(WhatsApp("+966501234567"), CancellationToken.None);

        Assert.Equal(customer.Id, result.CustomerId);
        Assert.False(result.NewCustomer);
        Assert.Empty(_customers.Created);
    }

    [Fact]
    public async Task UnknownWhatsAppNumber_CreatesCustomer_WithWhatsAppContact()
    {
        var result = await CreateProcessor().ProcessAsync(WhatsApp("+966501234567", name: "Nour Ali"), CancellationToken.None);

        var request = Assert.Single(_customers.Created);
        Assert.Equal(("Nour Ali", (string?)null, "+966501234567"), (request.Name, request.Email, request.Phone));
        var (customerId, contact) = Assert.Single(_customers.ContactsAdded);
        Assert.Equal(result.CustomerId, customerId);
        Assert.Equal(("whatsapp", "+966501234567", (bool?)true), (contact.Type, contact.Value, contact.IsPrimary));
        Assert.True(result.NewCustomer);
        Assert.Equal("+966501234567", Assert.Single(_received.Messages).From);
    }

    [Fact]
    public async Task UnknownWhatsAppNumber_WithoutProfileName_IsNamedByTheNumber()
    {
        await CreateProcessor().ProcessAsync(WhatsApp("+966501234567", name: null), CancellationToken.None);

        Assert.Equal("+966501234567", Assert.Single(_customers.Created).Name);
    }

    [Fact]
    public async Task UnparseableSender_IsStoredWithoutCustomer()
    {
        var result = await CreateProcessor().ProcessAsync(Email("not-an-address"), CancellationToken.None);

        Assert.Null(result.CustomerId);
        Assert.Empty(_customers.Created);
        Assert.Null(Assert.Single(_received.Messages).CustomerId);
    }
}
