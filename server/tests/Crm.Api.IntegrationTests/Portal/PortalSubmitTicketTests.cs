using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;

namespace Crm.Api.IntegrationTests.Portal;

public class PortalSubmitTicketTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    internal sealed record PortalTicketBody(Guid Id, string Number, string Subject, string Status, DateTime CreatedAt, AttachmentBody[] Attachments);

    internal sealed record AttachmentBody(Guid Id, string FileName, string ContentType, long Size);

    private PortalApp Portal => PortalApp.Create(factory);

    private static MultipartFormDataContent Form(string? subject, string? description = "It prints blank pages.", Guid? categoryId = null, params (string Name, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent();
        if (subject is not null)
        {
            form.Add(new StringContent(subject), "subject");
        }

        if (description is not null)
        {
            form.Add(new StringContent(description), "description");
        }

        if (categoryId is not null)
        {
            form.Add(new StringContent(categoryId.Value.ToString()), "categoryId");
        }

        foreach (var (name, bytes) in files)
        {
            form.Add(new ByteArrayContent(bytes), "files", name);
        }

        return form;
    }

    [Fact]
    public async Task Submitting_CreatesAPortalTicket_WithNumberAttachmentsAndConfirmationEmail()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var category = await TicketArrange.CategoryAsync(staff);
        var email = PortalApp.NewEmail();
        var (token, customer) = await portal.SignInAsync(email);
        var pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 hello");

        var response = await portal.Authenticated(token).PostAsync("/api/portal/tickets",
            Form("Printer is broken", "It prints blank pages.", category.Id, ("invoice.pdf", pdf)));
        var ticket = await response.Content.ReadFromJsonAsync<PortalTicketBody>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Matches("^TKT-[0-9]{6}$", ticket!.Number);
        Assert.Equal("new", ticket.Status);
        Assert.Equal("invoice.pdf", Assert.Single(ticket.Attachments).FileName);

        var staffView = await staff.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");
        Assert.Equal("portal", staffView!.Channel);
        Assert.Equal(customer.Id, staffView.CustomerId);
        Assert.Equal(category.Id, staffView.CategoryId);
        Assert.Equal("Printer is broken", staffView.Subject);

        var mail = portal.Email.Sent.Last(m => m.Recipient == email);
        Assert.Contains($"[{ticket.Number}]", mail.Subject);
        Assert.Contains(ticket.Number, mail.Body);

        var listed = await staff.GetFromJsonAsync<AttachmentBody[]>($"/api/tickets/{ticket.Id}/attachments");
        Assert.Equal("invoice.pdf", Assert.Single(listed!).FileName);
        var download = await staff.GetAsync($"/api/tickets/{ticket.Id}/attachments/{listed![0].Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ASubmittedTicket_StartsTheSlaTimers()
    {
        var portal = Portal;
        var (token, _) = await portal.SignInAsync();

        var created = await (await portal.Authenticated(token).PostAsync("/api/portal/tickets", Form("Help me")))
            .Content.ReadFromJsonAsync<PortalTicketBody>();
        var json = await (await portal.StaffAsync()).GetFromJsonAsync<JsonElement>($"/api/tickets/{created!.Id}");

        Assert.NotEqual(JsonValueKind.Null, json.GetProperty("responseDueAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, json.GetProperty("resolutionDueAt").ValueKind);
        Assert.Equal(created.CreatedAt, json.GetProperty("createdAt").GetDateTime());
    }

    [Fact]
    public async Task TicketsAreNumberedInSequence_AndShowInTheCustomerTimeline()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var (token, customer) = await portal.SignInAsync();
        var client = portal.Authenticated(token);

        var first = await (await client.PostAsync("/api/portal/tickets", Form("One"))).Content.ReadFromJsonAsync<PortalTicketBody>();
        var second = await (await client.PostAsync("/api/portal/tickets", Form("Two"))).Content.ReadFromJsonAsync<PortalTicketBody>();
        var timeline = await staff.GetStringAsync($"/api/customers/{customer.Id}/timeline");

        Assert.NotEqual(first!.Number, second!.Number);
        Assert.Contains(first.Number, timeline);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMissingSubject_Returns400_WithTheSubjectField(string? subject)
    {
        var portal = Portal;
        var (token, _) = await portal.SignInAsync();

        var response = await portal.Authenticated(token).PostAsync("/api/portal/tickets", Form(subject));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("subject", out _));
    }

    [Fact]
    public async Task ABadFile_Returns400_OnFiles_AndNoTicketIsCreated()
    {
        var portal = Portal;
        var (token, customer) = await portal.SignInAsync();
        var client = portal.Authenticated(token);

        var response = await client.PostAsync("/api/portal/tickets", Form("Hi", files: [("run.exe", [1, 2, 3])]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("files", out _));
        var tickets = await (await portal.StaffAsync()).GetFromJsonAsync<JsonElement>($"/api/tickets?search=Hi&pageSize=100");
        Assert.DoesNotContain(tickets.GetProperty("items").EnumerateArray(), t => t.GetProperty("customerId").GetGuid() == customer.Id);
    }

    [Fact]
    public async Task AnUnknownCategory_Returns400_OnCategoryId()
    {
        var portal = Portal;
        var (token, _) = await portal.SignInAsync();

        var response = await portal.Authenticated(token).PostAsync("/api/portal/tickets", Form("Hi", categoryId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ThePortalListsTheActiveCategories()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var category = await TicketArrange.CategoryAsync(staff, "Portal cat");
        var (token, _) = await portal.SignInAsync();

        var list = await portal.Authenticated(token).GetFromJsonAsync<JsonElement>("/api/portal/ticket-categories");

        Assert.Contains(list.EnumerateArray(), c => c.GetProperty("id").GetGuid() == category.Id && c.GetProperty("name").GetString() == category.Name);
    }

    [Fact]
    public async Task OnlyAPortalCustomerCanSubmit()
    {
        var portal = Portal;

        var anonymous = await portal.Anonymous().PostAsync("/api/portal/tickets", Form("Hi"));
        var staff = await (await portal.StaffAsync()).PostAsync("/api/portal/tickets", Form("Hi"));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);
    }

    [Fact]
    public async Task ACustomer_CannotReadTicketAttachmentsThroughTheStaffApi()
    {
        var portal = Portal;
        var (token, _) = await portal.SignInAsync();
        var client = portal.Authenticated(token);
        var ticket = await (await client.PostAsync("/api/portal/tickets", Form("Hi"))).Content.ReadFromJsonAsync<PortalTicketBody>();

        var response = await client.GetAsync($"/api/tickets/{ticket!.Id}/attachments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
