using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Customers;

/// <summary>CRM-11 AC 2–4: uploading (allowed types, at most 10 MB) and downloading customer files.</summary>
public class CustomerAttachmentsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const int TenMegabytes = 10 * 1024 * 1024;

    public sealed record AttachmentBody(
        Guid Id, string FileName, string ContentType, long Size, Guid? UploadedById, string? UploadedByName, DateTime UploadedAt);

    private async Task<(HttpClient Client, string Email)> AgentAsync()
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        return (factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword)), email);
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Nour Trading" });
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!.Id;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid customerId, string fileName, byte[] content,
        string browserContentType = "application/octet-stream")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(browserContentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return client.PostAsync($"/api/customers/{customerId}/attachments", form);
    }

    private static byte[] Bytes(int length) => [.. Enumerable.Range(0, length).Select(i => (byte)(i % 251))];

    [Theory]
    [InlineData("report.pdf", "application/pdf")]
    [InlineData("photo.PNG", "image/png")]
    [InlineData("contract.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task Upload_AllowedFile_IsSaved_AndDownloadable(string fileName, string contentType)
    {
        var (agent, email) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);
        var content = Bytes(5000);

        var response = await UploadAsync(agent, customerId, fileName, content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attachment = (await response.Content.ReadFromJsonAsync<AttachmentBody>())!;
        Assert.Equal($"/api/customers/{customerId}/attachments/{attachment.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal((fileName, contentType, 5000L, email), (attachment.FileName, attachment.ContentType, attachment.Size, attachment.UploadedByName));

        var list = await agent.GetFromJsonAsync<AttachmentBody[]>($"/api/customers/{customerId}/attachments");
        Assert.Equal(attachment, Assert.Single(list!));

        var download = await agent.GetAsync($"/api/customers/{customerId}/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(contentType, download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(fileName, download.Content.Headers.ContentDisposition?.FileNameStar);

        var timeline = await agent.GetFromJsonAsync<CustomerTimelineTests.TimelinePageBody>(
            $"/api/customers/{customerId}/timeline?type=attachment");
        Assert.Equal(("attachmentAdded", fileName), (timeline!.Items[0].Event, timeline.Items[0].Details));
    }

    [Fact]
    public async Task Upload_Exactly10MB_IsAccepted()
    {
        var (agent, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);

        var response = await UploadAsync(agent, customerId, "big.pdf", new byte[TenMegabytes]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(TenMegabytes, (await response.Content.ReadFromJsonAsync<AttachmentBody>())!.Size);
    }

    [Fact]
    public async Task Upload_Over10MB_Returns400()
    {
        var (agent, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);

        var response = await UploadAsync(agent, customerId, "big.pdf", new byte[TenMegabytes + 1]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["The file is larger than 10 MB."], problem!.Errors["file"]);
        Assert.Empty(await agent.GetFromJsonAsync<AttachmentBody[]>($"/api/customers/{customerId}/attachments") ?? []);
    }

    [Theory]
    [InlineData("setup.exe", "application/pdf")] // the browser's content type is not trusted
    [InlineData("run.bat", "text/plain")]
    [InlineData("script.js", "text/javascript")]
    [InlineData("invoice.pdf.exe", "application/octet-stream")]
    public async Task Upload_DisallowedType_Returns400(string fileName, string browserContentType)
    {
        var (agent, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);

        var response = await UploadAsync(agent, customerId, fileName, Bytes(100), browserContentType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["file"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task Upload_WithoutFile_Returns400()
    {
        var (agent, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);

        var response = await agent.PostAsync($"/api/customers/{customerId}/attachments",
            new MultipartFormDataContent { { new StringContent("x"), "other" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["file"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task Download_UnknownOrOtherCustomersAttachment_Returns404()
    {
        var (agent, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);
        var otherCustomerId = await CreateCustomerAsync(agent);
        var uploaded = await UploadAsync(agent, customerId, "report.pdf", Bytes(10));
        var attachment = (await uploaded.Content.ReadFromJsonAsync<AttachmentBody>())!;

        var otherCustomer = await agent.GetAsync($"/api/customers/{otherCustomerId}/attachments/{attachment.Id}");
        var unknown = await agent.GetAsync($"/api/customers/{customerId}/attachments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, otherCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Upload_StoresTheFileUnderTheConfiguredRoot_WithoutTheFileNameInThePath()
    {
        var (agent, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);

        var response = await UploadAsync(agent, customerId, "..\\..\\evil.pdf", Bytes(10));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attachment = (await response.Content.ReadFromJsonAsync<AttachmentBody>())!;
        Assert.Equal("evil.pdf", attachment.FileName);
        var stored = Path.Combine(factory.FilesRoot, "customers", customerId.ToString("N"), attachment.Id.ToString("N"));
        Assert.True(File.Exists(stored), stored);
        Assert.DoesNotContain(Directory.GetFiles(factory.FilesRoot, "*", SearchOption.AllDirectories), path => path.Contains("evil"));
    }
}
