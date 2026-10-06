using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Channels;

namespace Crm.UnitTests.Channels;

/// <summary>Received messages in memory; <see cref="ExistsAsync"/> sees saved and unsaved rows.</summary>
internal sealed class FakeReceivedMessageRepository : IReceivedMessageRepository
{
    public List<ReceivedMessage> Messages { get; } = [];

    /// <summary>Simulates a concurrent insert of the same message: the next save reports a duplicate.</summary>
    public bool NextSaveIsDuplicate { get; set; }

    public Task<bool> ExistsAsync(ChannelKind channel, string externalId, CancellationToken cancellationToken) =>
        Task.FromResult(Messages.Any(m => m.Channel == channel && m.ExternalId == externalId));

    public Task<DateTime?> LastReceivedAtAsync(ChannelKind channel, string from, CancellationToken cancellationToken) =>
        Task.FromResult(Messages.Where(m => m.Channel == channel && m.From == from).Max(m => (DateTime?)m.ReceivedAt));

    public void Add(ReceivedMessage message) => Messages.Add(message);

    public Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (!NextSaveIsDuplicate)
        {
            return Task.FromResult(true);
        }

        NextSaveIsDuplicate = false;
        Messages.RemoveAt(Messages.Count - 1);
        return Task.FromResult(false);
    }
}

/// <summary>
/// Customers in memory for the channel tests: lookup by exact email / E.164 number (phone or WhatsApp contact), create
/// and add contact. Other operations are not used by the channels.
/// </summary>
internal sealed class FakeCustomerService : ICustomerService
{
    public List<CustomerResponse> Customers { get; } = [];

    public List<CustomerRequest> Created { get; } = [];

    public List<(Guid CustomerId, CustomerContactRequest Request)> ContactsAdded { get; } = [];

    public CustomerResponse AddCustomer(string name, string? email = null, string? phone = null, string? whatsApp = null)
    {
        List<CustomerContactResponse> contacts = [];
        if (phone is not null)
        {
            contacts.Add(new CustomerContactResponse(Guid.NewGuid(), "phone", phone, true));
        }

        if (email is not null)
        {
            contacts.Add(new CustomerContactResponse(Guid.NewGuid(), "email", email, true));
        }

        if (whatsApp is not null)
        {
            contacts.Add(new CustomerContactResponse(Guid.NewGuid(), "whatsapp", whatsApp, true));
        }

        var customer = new CustomerResponse(Guid.NewGuid(), name, email, phone, DateTime.UtcNow, DateTime.UtcNow, contacts);
        Customers.Add(customer);
        return customer;
    }

    public Task<IReadOnlyList<CustomerResponse>> LookupAsync(CustomerLookupQuery query, CancellationToken cancellationToken)
    {
        if (query.Email is { } email && !email.Contains('@', StringComparison.Ordinal))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["email"] = ["invalid"] });
        }

        if (query.Phone is { } phone && !ContactValues.TryNormalizePhone(phone, out _))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["phone"] = ["invalid"] });
        }

        IReadOnlyList<CustomerResponse> found = [.. Customers
            .Where(c => c.Contacts.Any(contact => query.Email is not null
                ? contact.Type == "email" && contact.Value == query.Email.ToLowerInvariant()
                : contact.Type != "email" && ContactValues.TryNormalizePhone(query.Phone, out var e164) && contact.Value == e164))
            .OrderBy(c => c.Name, StringComparer.Ordinal)];
        return Task.FromResult(found);
    }

    public Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        Created.Add(request);
        return Task.FromResult(AddCustomer(request.Name!, request.Email, request.Phone));
    }

    public Task<CustomerContactResponse> AddContactAsync(Guid customerId, CustomerContactRequest request, CancellationToken cancellationToken)
    {
        ContactsAdded.Add((customerId, request));
        return Task.FromResult(new CustomerContactResponse(Guid.NewGuid(), request.Type!, request.Value!, request.IsPrimary ?? false));
    }

    public Task<PagedResult<CustomerResponse>> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CustomerResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.First(c => c.Id == id));

    public Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task MakeContactPrimaryAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RemoveContactAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>Records the inbound messages handed to the ticket step and answers with a fixed ticket.</summary>
internal sealed class FakeChannelTicketService : Crm.Application.Tickets.IChannelTicketService
{
    public List<(Guid CustomerId, InboundChannelMessage Message, int? TicketNumber)> Calls { get; } = [];

    public Guid TicketId { get; } = Guid.NewGuid();

    public bool Created { get; set; } = true;

    public Task<Crm.Application.Tickets.ChannelTicketResult> AddInboundAsync(
        Guid customerId, InboundChannelMessage message, int? ticketNumber, CancellationToken cancellationToken)
    {
        Calls.Add((customerId, message, ticketNumber));
        return Task.FromResult(new Crm.Application.Tickets.ChannelTicketResult(TicketId, Created));
    }
}
