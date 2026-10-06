using Crm.Application.Common.Exceptions;
using Crm.Application.Customers.Attachments;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-11 AC 2–4: uploading and downloading customer files.</summary>
public class CustomerAttachmentServiceTests
{
    private static readonly byte[] Pdf = "%PDF-1.7 test"u8.ToArray();

    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly Customer _customer = Customer.Create("Nour", null, null, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeAttachmentRepository _attachments = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FakeInteractionRecorder _timeline = new();
    private readonly CustomerAttachmentService _service;

    public CustomerAttachmentServiceTests()
    {
        _service = new CustomerAttachmentService(new OneCustomerRepository(_customer), _attachments, _storage, _timeline,
            new FakeCurrentUser(_userId), _clock, new UploadAttachmentRequestValidator());
    }

    private Task<CustomerAttachmentResponse> UploadAsync(string fileName, byte[] content, long? length = null) =>
        _service.UploadAsync(_customer.Id, new UploadAttachmentRequest(fileName, length ?? content.Length, new MemoryStream(content)),
            CancellationToken.None);

    [Fact]
    public async Task Upload_StoresTheFile_SavesTheRow_AndRecordsAttachmentAdded()
    {
        var response = await UploadAsync(@"C:\Users\me\report.pdf", Pdf);

        Assert.Equal(("report.pdf", "application/pdf", (long)Pdf.Length, (Guid?)_userId, _clock.UtcNow.UtcDateTime),
            (response.FileName, response.ContentType, response.Size, response.UploadedById, response.UploadedAt));
        var row = Assert.Single(_attachments.Attachments);
        Assert.Equal(Pdf, _storage.Files[row.StorageKey]);
        Assert.Equal(1, _attachments.SaveCount);
        Assert.Equal(
            [(_customer.Id, InteractionType.Attachment, InteractionEvents.AttachmentAdded, (string?)"report.pdf", (Guid?)row.Id, _clock.UtcNow.UtcDateTime)],
            _timeline.Entries);
    }

    [Fact]
    public async Task Upload_TooLarge_ThrowsValidationException_AndStoresNothing()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            UploadAsync("report.pdf", Pdf, AttachmentRules.MaxSizeBytes + 1));

        Assert.Equal(["file"], error.Errors.Keys);
        Assert.Empty(_storage.Files);
        Assert.Empty(_attachments.Attachments);
    }

    [Fact]
    public async Task Upload_DisallowedType_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => UploadAsync("setup.exe", Pdf));

        Assert.Equal(["file"], error.Errors.Keys);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Upload_ToUnknownCustomer_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UploadAsync(
            Guid.NewGuid(), new UploadAttachmentRequest("report.pdf", Pdf.Length, new MemoryStream(Pdf)), CancellationToken.None));

        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Download_ReturnsTheStoredFile()
    {
        var uploaded = await UploadAsync("report.pdf", Pdf);

        var download = await _service.DownloadAsync(_customer.Id, uploaded.Id, CancellationToken.None);

        using var buffer = new MemoryStream();
        await download.Content.CopyToAsync(buffer);
        Assert.Equal(Pdf, buffer.ToArray());
        Assert.Equal(("application/pdf", "report.pdf"), (download.ContentType, download.FileName));
    }

    [Fact]
    public async Task Download_OfAnotherCustomersAttachment_ThrowsNotFound()
    {
        var uploaded = await UploadAsync("report.pdf", Pdf);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DownloadAsync(Guid.NewGuid(), uploaded.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DownloadAsync(_customer.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Download_WhenTheFileIsMissing_ThrowsNotFound()
    {
        var uploaded = await UploadAsync("report.pdf", Pdf);
        _storage.Files.Clear();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DownloadAsync(_customer.Id, uploaded.Id, CancellationToken.None));
    }
}
