using Crm.Application.Common.Files;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Customers.Notes;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>Records every <see cref="IInteractionRecorder.Record"/> call.</summary>
internal sealed class FakeInteractionRecorder : IInteractionRecorder
{
    public List<(Guid CustomerId, InteractionType Type, string Event, string? Details, Guid? SourceId, DateTime UtcNow)> Entries { get; } = [];

    public void Record(Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, DateTime utcNow) =>
        Entries.Add((customerId, type, @event, details, sourceId, utcNow));
}

/// <summary>Timeline storage in memory: keeps added entries and answers the last list call with a fixed page.</summary>
internal sealed class FakeTimelineRepository : ICustomerTimelineRepository
{
    public List<CustomerInteraction> Added { get; } = [];

    public (Guid CustomerId, InteractionType? Type, int Page, int PageSize)? LastList { get; private set; }

    public void Add(CustomerInteraction interaction) => Added.Add(interaction);

    public Task<PagedResult<CustomerInteractionResponse>> ListAsync(
        Guid customerId, InteractionType? type, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastList = (customerId, type, page, pageSize);
        return Task.FromResult(new PagedResult<CustomerInteractionResponse>([], page, pageSize, 0));
    }
}

/// <summary>The signed-in user of a unit test (null = anonymous / system).</summary>
internal sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public bool IsInRole(string role) => false;

    public bool HasPermission(string permission) => false;
}

/// <summary>A clock the test sets by hand.</summary>
internal sealed class ManualClock(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>A customer repository that knows one customer (only <see cref="FindAsync"/> is used by the child features).</summary>
internal sealed class OneCustomerRepository(Customer customer) : ICustomerRepository
{
    public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(id == customer.Id ? customer : null);

    public Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Customer>> FindByContactAsync(
        IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public void Add(Customer customer) => throw new NotSupportedException();

    public Task MoveTicketsToBranchAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>Notes in memory; the author name is "User &lt;id&gt;".</summary>
internal sealed class FakeNoteRepository : ICustomerNoteRepository
{
    public List<CustomerNote> Notes { get; } = [];

    public int SaveCount { get; private set; }

    public void Add(CustomerNote note) => Notes.Add(note);

    public Task<CustomerNoteResponse?> GetAsync(Guid noteId, CancellationToken cancellationToken) =>
        Task.FromResult(Notes.Where(n => n.Id == noteId).Select(ToResponse).FirstOrDefault());

    public Task<PagedResult<CustomerNoteResponse>> ListAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        List<CustomerNoteResponse> items = [.. Notes.Where(n => n.CustomerId == customerId).OrderByDescending(n => n.CreatedAt).Select(ToResponse)];
        return Task.FromResult(new PagedResult<CustomerNoteResponse>(items, page, pageSize, items.Count));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    private static CustomerNoteResponse ToResponse(CustomerNote note) =>
        new(note.Id, note.Text, note.AuthorId, note.AuthorId is null ? null : $"User {note.AuthorId}", note.CreatedAt);
}

/// <summary>Attachments in memory.</summary>
internal sealed class FakeAttachmentRepository : ICustomerAttachmentRepository
{
    public List<CustomerAttachment> Attachments { get; } = [];

    public int SaveCount { get; private set; }

    public void Add(CustomerAttachment attachment) => Attachments.Add(attachment);

    public Task<CustomerAttachment?> FindAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Attachments.FirstOrDefault(a => a.CustomerId == customerId && a.Id == attachmentId));

    public Task<CustomerAttachmentResponse?> GetAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Attachments.Where(a => a.Id == attachmentId).Select(ToResponse).FirstOrDefault());

    public Task<IReadOnlyList<CustomerAttachmentResponse>> ListAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CustomerAttachmentResponse>>([.. Attachments.Where(a => a.CustomerId == customerId).Select(ToResponse)]);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    private static CustomerAttachmentResponse ToResponse(CustomerAttachment a) =>
        new(a.Id, a.FileName, a.ContentType, a.Size, a.UploadedById, null, a.UploadedAt);
}

/// <summary>Files in memory, by key.</summary>
internal sealed class FakeFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Files.Add(key, buffer.ToArray());
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }
}
