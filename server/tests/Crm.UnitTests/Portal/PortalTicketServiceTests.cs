using Crm.Application.Common.Exceptions;
using Crm.Application.Customers.Attachments;
using Crm.Application.Portal;
using Crm.Application.Tickets;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using Crm.UnitTests.Customers;
using Crm.UnitTests.Sla;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Portal;

public class PortalTicketServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 test");

    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeSlaPolicyRepository _policies = new(Start.UtcDateTime.AddDays(-1));
    private readonly Crm.UnitTests.Tickets.FakeInteractionRecorder _timeline = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FakeTicketAttachmentRepository _attachments = new();
    private readonly RecordingSender _sender = new();
    private readonly FakeCustomers _customers = new();
    private readonly TestClock _clock = new(Start);
    private readonly Customer _customer;
    private readonly PortalTicketService _service;

    public PortalTicketServiceTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _customer = Customer.Create("Nour Trading", "nour@customer.example", null, Start.UtcDateTime);
        _customers.Customers.Add(_customer);
        _tickets.Customers[_customer.Id] = _customer.Name;
        _tickets.AllCustomerNames[_customer.Id] = _customer.Name;
        var tickets = new TicketService(_tickets, _categories, _timeline, new Crm.UnitTests.Tickets.FakeCurrentUser(null), _clock,
            new CreateTicketRequestValidator(), new ListTicketsQueryValidator(), _policies, new FakeTicketHistoryRecorder());
        _service = new PortalTicketService(
            tickets, _attachments, _storage, _sender, _customers, _categories, _clock,
            new PortalSubmitTicketRequestValidator(new UploadAttachmentRequestValidator()));
    }

    private static UploadAttachmentRequest File(string name = "invoice.pdf", byte[]? bytes = null)
    {
        bytes ??= Pdf;
        return new UploadAttachmentRequest(name, bytes.Length, new MemoryStream(bytes));
    }

    private Task<PortalTicketResponse> SubmitAsync(string? subject = "Printer is broken", Guid? categoryId = null, params UploadAttachmentRequest[] files) =>
        _service.SubmitAsync(_customer.Id, new PortalSubmitTicketRequest(subject, "It prints blank pages.", categoryId, files), CancellationToken.None);

    [Fact]
    public async Task Submit_CreatesAPortalTicket_WithoutAStaffCreator()
    {
        var response = await SubmitAsync();

        var ticket = Assert.Single(_tickets.Tickets);
        Assert.Equal(TicketChannel.Portal, ticket.Channel);
        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal(TicketPriority.Mid, ticket.Priority);
        Assert.Null(ticket.CreatedById);
        Assert.Equal(_customer.Id, ticket.CustomerId);
        Assert.Equal("Printer is broken", ticket.Subject);
        Assert.Equal("It prints blank pages.", ticket.Description);
        Assert.Equal(ticket.Id, response.Id);
        Assert.Equal("TKT-000001", response.Number);
    }

    [Fact]
    public async Task Submit_StartsTheSlaTimers()
    {
        await SubmitAsync();

        var policy = _policies.Policies.Single(p => p.Priority == TicketPriority.Mid);
        var ticket = Assert.Single(_tickets.Tickets);
        Assert.Equal(policy.ResponseDueAt(Start.UtcDateTime), ticket.ResponseDueAt);
        Assert.Equal(policy.ResolutionDueAt(Start.UtcDateTime), ticket.ResolutionDueAt);
    }

    [Fact]
    public async Task Submit_AddsTheTicketToTheCustomerTimeline()
    {
        await SubmitAsync();

        Assert.Contains(_timeline.Entries, e => e.CustomerId == _customer.Id && e.Type == InteractionType.Ticket);
    }

    [Fact]
    public async Task Submit_SendsAConfirmationEmail_WithTheTicketNumberTag()
    {
        var response = await SubmitAsync();

        var mail = Assert.Single(_sender.Sent);
        Assert.Equal("nour@customer.example", mail.Recipient);
        Assert.Equal(Crm.Domain.Channels.ChannelKind.Email, mail.Channel);
        Assert.Contains("[TKT-000001]", mail.Subject);
        Assert.Contains("TKT-000001", mail.Body);
        Assert.Equal(response.Id, mail.SourceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_WithoutASubject_ThrowsValidation_AndStoresNothing(string? subject)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => SubmitAsync(subject, null, File()));

        Assert.Contains("subject", error.Errors.Keys);
        Assert.Empty(_tickets.Tickets);
        Assert.Empty(_storage.Files);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Submit_WithAnUnknownOrInactiveCategory_ThrowsValidation_OnCategoryId()
    {
        var inactive = TicketCategory.Create("Old", Start.UtcDateTime);
        inactive.Update("Old", false, Start.UtcDateTime);
        _categories.Categories.Add(inactive);

        var unknown = await Assert.ThrowsAsync<ValidationException>(() => SubmitAsync(categoryId: Guid.NewGuid()));
        var off = await Assert.ThrowsAsync<ValidationException>(() => SubmitAsync(categoryId: inactive.Id));

        Assert.Contains("categoryId", unknown.Errors.Keys);
        Assert.Contains("categoryId", off.Errors.Keys);
        Assert.Empty(_tickets.Tickets);
    }

    [Fact]
    public async Task Submit_WithAnActiveCategory_KeepsIt()
    {
        var category = TicketCategory.Create("Billing", Start.UtcDateTime);
        _categories.Categories.Add(category);

        await SubmitAsync(categoryId: category.Id);

        Assert.Equal(category.Id, _tickets.Tickets.Single().CategoryId);
    }

    [Fact]
    public async Task Submit_StoresTheAttachments_WithTheTicket()
    {
        var response = await SubmitAsync(files: [File("invoice.pdf"), File("photo.png", [1, 2, 3])]);

        Assert.Equal(2, _attachments.Items.Count);
        Assert.All(_attachments.Items, a => Assert.Equal(response.Id, a.TicketId));
        Assert.Equal(["invoice.pdf", "photo.png"], _attachments.Items.Select(a => a.FileName).Order());
        Assert.Equal(2, _storage.Files.Count);
        Assert.Equal(Pdf, _storage.Files[_attachments.Items.Single(a => a.FileName == "invoice.pdf").StorageKey]);
        Assert.Equal(["invoice.pdf", "photo.png"], response.Attachments.Select(a => a.FileName).Order());
    }

    [Fact]
    public async Task Submit_WithABadFile_ThrowsValidation_OnFiles_AndStoresNothing()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => SubmitAsync(files: [File("virus.exe")]));

        Assert.Contains("files", error.Errors.Keys);
        Assert.Empty(_tickets.Tickets);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Submit_WithTooManyFiles_ThrowsValidation_OnFiles()
    {
        var files = Enumerable.Range(0, PortalSubmitTicketRequestValidator.MaxFiles + 1).Select(i => File($"f{i}.pdf")).ToArray();

        var error = await Assert.ThrowsAsync<ValidationException>(() => SubmitAsync(files: files));

        Assert.Contains("files", error.Errors.Keys);
        Assert.Empty(_tickets.Tickets);
    }

    [Fact]
    public async Task Submit_ForAnUnknownCustomer_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SubmitAsync(
            Guid.NewGuid(), new PortalSubmitTicketRequest("Hi", null, null, []), CancellationToken.None));

    [Fact]
    public async Task ThePortalCategories_AreTheActiveOnes()
    {
        _categories.Categories.Add(TicketCategory.Create("Billing", Start.UtcDateTime));
        var off = TicketCategory.Create("Old", Start.UtcDateTime);
        off.Update("Old", false, Start.UtcDateTime);
        _categories.Categories.Add(off);

        var list = await _service.ListCategoriesAsync(CancellationToken.None);

        Assert.Equal(["Billing"], list.Select(c => c.Name));
    }
}
