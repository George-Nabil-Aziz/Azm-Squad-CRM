using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;

namespace Crm.Api.IntegrationTests.Portal;

public class PortalTrackTicketsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    internal sealed record SummaryBody(
        Guid Id, string Number, string Subject, string? Description, string Status, string? CategoryName, DateTime CreatedAt, DateTime UpdatedAt,
        bool CanReply, bool CanReopen);

    internal sealed record PageBody(SummaryBody[] Items, int Page, int PageSize, int TotalCount);

    internal sealed record MessageBody(Guid Id, bool FromCustomer, string? AuthorName, string Body, DateTime CreatedAt);

    internal sealed record HistoryBody(string Type, string? Status, DateTime At);

    private PortalApp Portal => PortalApp.Create(factory);

    private static async Task<SummaryBody> SubmitAsync(HttpClient customer, string subject = "Printer is broken")
    {
        using var form = new MultipartFormDataContent { { new StringContent(subject), "subject" }, { new StringContent("details"), "description" } };
        var response = await customer.PostAsync("/api/portal/tickets", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<JsonElement>());
        return (await customer.GetFromJsonAsync<SummaryBody>($"/api/portal/tickets/{created.GetProperty("id").GetGuid()}"))!;
    }

    private static async Task SetStatusAsync(HttpClient staff, Guid ticketId, params string[] statuses)
    {
        foreach (var status in statuses)
        {
            var response = await staff.PutAsJsonAsync($"/api/tickets/{ticketId}/status", new { status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task ACustomerSeesOnlyTheirOwnTickets_AndAnotherCustomersTicketIs404()
    {
        var portal = Portal;
        var (tokenA, _) = await portal.SignInAsync();
        var (tokenB, _) = await portal.SignInAsync();
        var a = portal.Authenticated(tokenA);
        var b = portal.Authenticated(tokenB);
        var mine = await SubmitAsync(a, "Mine");
        var theirs = await SubmitAsync(b, "Theirs");

        var list = await a.GetFromJsonAsync<PageBody>("/api/portal/tickets");

        Assert.Equal([mine.Id], list!.Items.Select(t => t.Id));
        Assert.Equal(1, list.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/portal/tickets/{theirs.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/portal/tickets/{theirs.Id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/portal/tickets/{theirs.Id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync($"/api/portal/tickets/{theirs.Id}/messages", new { body = "hi" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync($"/api/portal/tickets/{theirs.Id}/reopen", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/portal/tickets/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task TheCustomerSeesStatusAndPublicRepliesOnly_NeverInternalNotes()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var (token, _) = await portal.SignInAsync();
        var customer = portal.Authenticated(token);
        var ticket = await SubmitAsync(customer);
        await staff.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages", new { body = "INTERNAL secret note", @internal = true });
        await staff.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages", new { body = "We are on it." });
        await SetStatusAsync(staff, ticket.Id, "open");

        var messages = await customer.GetStringAsync($"/api/portal/tickets/{ticket.Id}/messages");
        var detail = await customer.GetStringAsync($"/api/portal/tickets/{ticket.Id}");
        var parsed = JsonSerializer.Deserialize<MessageBody[]>(messages, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(["We are on it."], parsed!.Select(m => m.Body));
        Assert.DoesNotContain("INTERNAL", messages);
        Assert.DoesNotContain("secret", detail);
        Assert.Contains("\"status\":\"open\"", detail);
        Assert.DoesNotContain("assignee", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("priority", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("due", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheCustomerCanReply_AndStaffSeeTheReplyInTheThread()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var (token, _) = await portal.SignInAsync();
        var customer = portal.Authenticated(token);
        var ticket = await SubmitAsync(customer);

        var response = await customer.PostAsJsonAsync($"/api/portal/tickets/{ticket.Id}/messages", new { body = "Any news?" });
        var reply = await response.Content.ReadFromJsonAsync<MessageBody>();
        var thread = await staff.GetFromJsonAsync<JsonElement>($"/api/tickets/{ticket.Id}/messages");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(reply!.FromCustomer);
        Assert.Contains(thread.EnumerateArray(), m => m.GetProperty("body").GetString() == "Any news?" && m.GetProperty("direction").GetString() == "inbound"
                                                       && m.GetProperty("channel").GetString() == "portal");
        Assert.Equal(["Any news?"], (await customer.GetFromJsonAsync<MessageBody[]>($"/api/portal/tickets/{ticket.Id}/messages"))!.Select(m => m.Body));
    }

    [Fact]
    public async Task AReplyOnAPendingTicketReopensIt_AndAReplyOnAResolvedTicketIs400()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var (token, _) = await portal.SignInAsync();
        var customer = portal.Authenticated(token);
        var pending = await SubmitAsync(customer, "Pending one");
        var resolved = await SubmitAsync(customer, "Resolved one");
        await SetStatusAsync(staff, pending.Id, "open", "pending");
        await SetStatusAsync(staff, resolved.Id, "open", "resolved");

        await customer.PostAsJsonAsync($"/api/portal/tickets/{pending.Id}/messages", new { body = "Here is the info" });
        var rejected = await customer.PostAsJsonAsync($"/api/portal/tickets/{resolved.Id}/messages", new { body = "One more thing" });
        var blank = await customer.PostAsJsonAsync($"/api/portal/tickets/{pending.Id}/messages", new { body = " " });

        Assert.Equal("open", (await customer.GetFromJsonAsync<SummaryBody>($"/api/portal/tickets/{pending.Id}"))!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("status", out _));
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
    }

    [Fact]
    public async Task TheCustomerCanReopenAResolvedTicket_WithinTheAllowedDays()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var email = PortalApp.NewEmail();
        var (token, _) = await portal.SignInAsync(email);
        var customer = portal.Authenticated(token);
        var inTime = await SubmitAsync(customer, "In time");
        var late = await SubmitAsync(customer, "Too late");
        await SetStatusAsync(staff, inTime.Id, "open", "resolved");
        await SetStatusAsync(staff, late.Id, "open", "resolved");

        factory.Time.Advance(TimeSpan.FromDays(6));
        customer = portal.Authenticated((await portal.SignInAsync(email)).Token); // tokens last one hour
        var seen = await customer.GetFromJsonAsync<SummaryBody>($"/api/portal/tickets/{inTime.Id}");
        var reopened = await customer.PostAsync($"/api/portal/tickets/{inTime.Id}/reopen", null);
        factory.Time.Advance(TimeSpan.FromDays(2));
        customer = portal.Authenticated((await portal.SignInAsync(email)).Token); // the first token (1 hour) has expired
        var tooLate = await customer.PostAsync($"/api/portal/tickets/{late.Id}/reopen", null);
        var lateSeen = await customer.GetFromJsonAsync<SummaryBody>($"/api/portal/tickets/{late.Id}");

        Assert.True(seen!.CanReopen);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        Assert.Equal("open", (await reopened.Content.ReadFromJsonAsync<SummaryBody>())!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, tooLate.StatusCode);
        Assert.False(lateSeen!.CanReopen);
        Assert.Equal("resolved", lateSeen.Status);
        staff = await portal.StaffAsync(); // its token has expired too
        var staffView = await staff.GetFromJsonAsync<TicketBody>($"/api/tickets/{inTime.Id}");
        Assert.Equal("open", staffView!.Status);
        Assert.Null(staffView.ResolvedAt);
    }

    [Fact]
    public async Task ATicketThatIsNotResolvedCannotBeReopened()
    {
        var portal = Portal;
        var (token, _) = await portal.SignInAsync();
        var customer = portal.Authenticated(token);
        var ticket = await SubmitAsync(customer);

        Assert.Equal(HttpStatusCode.BadRequest, (await customer.PostAsync($"/api/portal/tickets/{ticket.Id}/reopen", null)).StatusCode);
    }

    [Fact]
    public async Task TheHistoryShowsCreationAndStatusChangesOnly()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var (token, _) = await portal.SignInAsync();
        var customer = portal.Authenticated(token);
        var ticket = await SubmitAsync(customer);
        await SetStatusAsync(staff, ticket.Id, "open");
        await staff.PutAsJsonAsync($"/api/tickets/{ticket.Id}/priority", new { priority = "high" });
        await SetStatusAsync(staff, ticket.Id, "resolved");
        await customer.PostAsync($"/api/portal/tickets/{ticket.Id}/reopen", null);

        var history = await customer.GetFromJsonAsync<HistoryBody[]>($"/api/portal/tickets/{ticket.Id}/history");

        Assert.Equal(["created", "status", "status", "status"], history!.Select(h => h.Type));
        Assert.Equal([null, "open", "resolved", "open"], history.Select(h => h.Status));
    }

    [Fact]
    public async Task OnlyAPortalCustomerCanUseTheTrackingEndpoints()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await portal.Anonymous().GetAsync("/api/portal/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/portal/tickets")).StatusCode);
    }
}
