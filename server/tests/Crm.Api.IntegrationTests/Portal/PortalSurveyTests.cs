using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Crm.Api.IntegrationTests.Infrastructure;

namespace Crm.Api.IntegrationTests.Portal;

public class PortalSurveyTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record TicketIdBody(Guid Id, string Number);

    private sealed record InfoBody(string TicketNumber, string Subject, string State, int? Rating, string? Comment, DateTime? ExpiresAt);

    private PortalApp Portal => PortalApp.Create(factory, new Dictionary<string, string?> { ["Portal:BaseUrl"] = "https://help.example.com" });

    private static async Task<TicketIdBody> SubmitAsync(HttpClient customer, string subject = "Printer is broken")
    {
        using var form = new MultipartFormDataContent { { new StringContent(subject), "subject" } };
        var response = await customer.PostAsync("/api/portal/tickets", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketIdBody>())!;
    }

    private static async Task ResolveAsync(HttpClient staff, Guid ticketId)
    {
        foreach (var status in new[] { "open", "resolved" })
        {
            Assert.Equal(HttpStatusCode.OK, (await staff.PutAsJsonAsync($"/api/tickets/{ticketId}/status", new { status })).StatusCode);
        }
    }

    private static string TokenFromMail(PortalApp portal, string email)
    {
        var mail = portal.Email.Sent.Last(m => m.Recipient == email && m.Body.Contains("/portal/survey/", StringComparison.Ordinal));
        return Regex.Match(mail.Body, @"https://help\.example\.com/portal/survey/(?<t>[A-Za-z0-9_\-]+)").Groups["t"].Value;
    }

    private async Task<(PortalApp Portal, HttpClient Staff, HttpClient Customer, string Email, TicketIdBody Ticket)> ResolvedTicketAsync()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var email = PortalApp.NewEmail();
        var (token, _) = await portal.SignInAsync(email);
        var customer = portal.Authenticated(token);
        var ticket = await SubmitAsync(customer);
        await ResolveAsync(staff, ticket.Id);
        return (portal, staff, customer, email, ticket);
    }

    [Fact]
    public async Task WhenATicketIsResolved_TheCustomerGetsAnEmailWithTheSurveyLink_AndASurveyInThePortal()
    {
        var (portal, _, customer, email, ticket) = await ResolvedTicketAsync();

        var token = TokenFromMail(portal, email);
        var viaLink = await portal.Anonymous().GetFromJsonAsync<InfoBody>($"/api/portal/surveys/{token}");
        var inPortal = await customer.GetFromJsonAsync<InfoBody>($"/api/portal/tickets/{ticket.Id}/feedback");

        Assert.True(token.Length >= 32);
        Assert.Equal(("open", ticket.Number), (viaLink!.State, viaLink.TicketNumber));
        Assert.Equal("open", inPortal!.State);
        Assert.NotNull(viaLink.ExpiresAt);
    }

    [Fact]
    public async Task ATicketThatIsNotResolved_HasNoSurvey()
    {
        var portal = Portal;
        var (token, _) = await portal.SignInAsync();
        var customer = portal.Authenticated(token);
        var ticket = await SubmitAsync(customer);

        var info = await customer.GetFromJsonAsync<InfoBody>($"/api/portal/tickets/{ticket.Id}/feedback");
        var rate = await customer.PostAsJsonAsync($"/api/portal/tickets/{ticket.Id}/feedback", new { rating = 5 });

        Assert.Equal("none", info!.State);
        Assert.Equal(HttpStatusCode.BadRequest, rate.StatusCode);
    }

    [Fact]
    public async Task ARating_WithAnOptionalComment_IsSavedOnce_ASecondSubmitIs400()
    {
        var (portal, _, _, email, _) = await ResolvedTicketAsync();
        var token = TokenFromMail(portal, email);
        var visitor = portal.Anonymous();

        var first = await visitor.PostAsJsonAsync($"/api/portal/surveys/{token}", new { rating = 4, comment = "Fast help" });
        var second = await visitor.PostAsJsonAsync($"/api/portal/surveys/{token}", new { rating = 1 });
        var info = await visitor.GetFromJsonAsync<InfoBody>($"/api/portal/surveys/{token}");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        using var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("rating", out _));
        Assert.Equal(("answered", 4, "Fast help"), (info!.State, info.Rating, info.Comment));
    }

    [Theory]
    [InlineData("{ \"rating\": 0 }")]
    [InlineData("{ \"rating\": 6 }")]
    [InlineData("{ \"rating\": -2 }")]
    [InlineData("{ \"comment\": \"no rating\" }")]
    public async Task ARatingOutsideOneToFive_Returns400_AndTheSurveyStaysOpen(string body)
    {
        var (portal, _, _, email, _) = await ResolvedTicketAsync();
        var token = TokenFromMail(portal, email);
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await portal.Anonymous().PostAsync($"/api/portal/surveys/{token}", content);
        var info = await portal.Anonymous().GetFromJsonAsync<InfoBody>($"/api/portal/surveys/{token}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("open", info!.State);
    }

    [Fact]
    public async Task TheSurveyLink_ExpiresAfterSevenDays()
    {
        var (portal, _, _, email, _) = await ResolvedTicketAsync();
        var token = TokenFromMail(portal, email);

        factory.Time.Advance(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1));
        var stillOpen = await portal.Anonymous().GetFromJsonAsync<InfoBody>($"/api/portal/surveys/{token}");
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        var expired = await portal.Anonymous().GetFromJsonAsync<InfoBody>($"/api/portal/surveys/{token}");
        var late = await portal.Anonymous().PostAsJsonAsync($"/api/portal/surveys/{token}", new { rating = 5 });

        Assert.Equal("open", stillOpen!.State);
        Assert.Equal("expired", expired!.State);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        using var problem = JsonDocument.Parse(await late.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("token", out _));
    }

    [Fact]
    public async Task AnUnknownToken_Is404()
    {
        var visitor = Portal.Anonymous();

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/api/portal/surveys/does-not-exist")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsJsonAsync("/api/portal/surveys/does-not-exist", new { rating = 5 })).StatusCode);
    }

    [Fact]
    public async Task TheSignedInCustomer_RatesTheirOwnTicket_ButNotSomeoneElses()
    {
        var (portal, _, customer, _, ticket) = await ResolvedTicketAsync();
        var (otherToken, _) = await portal.SignInAsync();
        var other = portal.Authenticated(otherToken);

        var foreign = await other.PostAsJsonAsync($"/api/portal/tickets/{ticket.Id}/feedback", new { rating = 5 });
        var own = await customer.PostAsJsonAsync($"/api/portal/tickets/{ticket.Id}/feedback", new { rating = 5, comment = "ok" });
        var again = await customer.PostAsJsonAsync($"/api/portal/tickets/{ticket.Id}/feedback", new { rating = 2 });

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/portal/tickets/{ticket.Id}/feedback")).StatusCode);
    }

    [Fact]
    public async Task ReopeningAndResolvingAgain_DoesNotAskAnAnsweredCustomerTwice_ButRenewsAnUnansweredSurvey()
    {
        var (portal, staff, customer, email, ticket) = await ResolvedTicketAsync();
        var mails = portal.Email.CountTo(email);
        var token = TokenFromMail(portal, email);

        await customer.PostAsync($"/api/portal/tickets/{ticket.Id}/reopen", null);
        await ResolveAgainAsync(staff, ticket.Id);
        var renewedMail = portal.Email.CountTo(email);
        Assert.Equal(token, TokenFromMail(portal, email)); // same link, new expiry
        await portal.Anonymous().PostAsJsonAsync($"/api/portal/surveys/{token}", new { rating = 5 });
        await customer.PostAsync($"/api/portal/tickets/{ticket.Id}/reopen", null);
        await ResolveAgainAsync(staff, ticket.Id);

        Assert.Equal(mails + 1, renewedMail);
        Assert.Equal(renewedMail, portal.Email.CountTo(email)); // answered: nothing more is sent
    }

    private static async Task ResolveAgainAsync(HttpClient staff, Guid ticketId) =>
        Assert.Equal(HttpStatusCode.OK, (await staff.PutAsJsonAsync($"/api/tickets/{ticketId}/status", new { status = "resolved" })).StatusCode);

    [Fact]
    public async Task ASubmittedRating_ShowsInTheCsatReport()
    {
        var (portal, staff, customer, email, ticket) = await ResolvedTicketAsync();
        var token = TokenFromMail(portal, email);
        var today = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime);

        await portal.Anonymous().PostAsJsonAsync($"/api/portal/surveys/{token}", new { rating = 2, comment = "Took too long" });
        var report = await staff.GetFromJsonAsync<JsonElement>($"/api/reports/csat?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");

        Assert.True(report.GetProperty("totalRatings").GetInt32() >= 1);
        Assert.True(report.GetProperty("surveysSent").GetInt32() >= 1);
        var low = report.GetProperty("lowRatings").EnumerateArray()
            .Single(r => r.GetProperty("ticketId").GetGuid() == ticket.Id);
        Assert.Equal(2, low.GetProperty("rating").GetInt32());
        Assert.Equal("Took too long", low.GetProperty("comment").GetString());
        Assert.Equal(ticket.Number, low.GetProperty("ticketNumber").GetString());
        Assert.NotNull(customer);
    }
}
