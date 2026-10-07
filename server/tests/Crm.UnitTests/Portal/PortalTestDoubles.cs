using Crm.Application.Auth;
using Crm.Application.Channels;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Application.Portal;
using Crm.Domain.Channels;
using Crm.Domain.Customers;
using Crm.Application.Tickets;
using Crm.Domain.Portal;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Portal;

/// <summary>Hands out the codes a test lists, then "000000".</summary>
internal sealed class FixedCodeGenerator(params string[] codes) : IPortalCodeGenerator
{
    private int _next;

    public string NewCode() => _next < codes.Length ? codes[_next++] : "000000";
}

/// <summary>Records what the portal sends; sending never fails.</summary>
internal sealed class RecordingSender : IChannelSender
{
    public List<ChannelReply> Sent { get; } = [];

    public Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken)
    {
        Sent.Add(reply);
        var now = DateTime.UtcNow;
        return Task.FromResult(new OutboundMessageResponse(Guid.NewGuid(), "email", reply.Recipient, "sent", 1, null, null, reply.SourceId, now, now));
    }

    public Task<bool> ApplyDeliveryStatusAsync(string providerMessageId, DeliveryStatus status, string? error, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task EnsureCanSendAsync(ChannelReply reply, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<int> RetryDueAsync(CancellationToken cancellationToken) => Task.FromResult(0);

    public ChannelStatusResponse GetStatus() => new(new ChannelState(true), new ChannelState(false));
}

internal sealed class FakeTokenGenerator : IAccessTokenGenerator
{
    public List<AccessTokenSubject> Subjects { get; } = [];

    public AccessToken Generate(AccessTokenSubject subject)
    {
        Subjects.Add(subject);
        return new AccessToken($"token-{subject.UserId}", DateTimeOffset.UtcNow.AddHours(1));
    }
}

internal sealed class FakeCustomers : ICustomerRepository
{
    public List<Customer> Customers { get; } = [];

    public Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(new PagedResult<Customer>(Customers, 1, Customers.Count, Customers.Count));

    public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.FirstOrDefault(c => c.Id == id && !c.IsDeleted));

    public Task<IReadOnlyList<Customer>> FindByContactAsync(
        IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(
            [.. Customers.Where(c => !c.IsDeleted && c.Contacts.Any(x => types.Contains(x.Type) && x.Value == value)).OrderBy(c => c.Name)]);

    public void Add(Customer customer) => Customers.Add(customer);

    public Task MoveTicketsToBranchAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakePortalAccounts : IPortalAccountRepository
{
    public List<PortalLoginCode> Codes { get; } = [];

    public List<PortalAccount> Accounts { get; } = [];

    public int Saves { get; private set; }

    public Task<PortalLoginCode?> FindLatestCodeAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Codes.Where(c => c.Email == email).OrderByDescending(c => c.CreatedAt).FirstOrDefault());

    public Task<IReadOnlyList<PortalLoginCode>> ListUnusedCodesAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PortalLoginCode>>([.. Codes.Where(c => c.Email == email && c.ConsumedAt is null)]);

    public void AddCode(PortalLoginCode code) => Codes.Add(code);

    public Task<PortalAccount?> FindAccountByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Email == email));

    public void AddAccount(PortalAccount account) => Accounts.Add(account);

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Any(a => a.CustomerId == customerId));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory ticket attachment storage.</summary>
internal sealed class FakeTicketAttachmentRepository : ITicketAttachmentRepository
{
    public List<TicketAttachment> Items { get; } = [];

    public void Add(TicketAttachment attachment) => Items.Add(attachment);

    public Task<IReadOnlyList<TicketAttachment>> ListAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TicketAttachment>>([.. Items.Where(a => a.TicketId == ticketId).OrderBy(a => a.UploadedAt)]);

    public Task<TicketAttachment?> FindAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(a => a.TicketId == ticketId && a.Id == attachmentId));

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
