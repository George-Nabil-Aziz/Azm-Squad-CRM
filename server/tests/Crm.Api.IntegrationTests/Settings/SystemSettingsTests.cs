using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Sla;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Crm.Application.Channels;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Settings;

public class SystemSettingsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string AccessToken = "EAAG-super-secret-access-token";
    private const string SmtpPassword = "smtp-Secret#Pw1";

    private async Task<HttpClient> SuperAdminAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    private static object WorkWeek(bool enabled = true) =>
        new { enabled, days = new[] { "sunday", "monday", "tuesday", "wednesday", "thursday" }, start = "08:00", end = "16:00" };

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, object body) =>
        await client.PutAsJsonAsync("/api/settings", body);

    [Fact]
    public async Task Get_ReturnsTheDefaults_ForSuperAdmin()
    {
        var response = await (await SuperAdminAsync()).GetAsync("/api/settings");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"timeZone\":\"Asia/Riyadh\"", text);
        Assert.Contains("\"secretsSet\"", text);
    }

    [Fact]
    public async Task Put_SavesBusinessHoursTimeZoneAndPrefix_AndGetReturnsThem()
    {
        var superAdmin = await SuperAdminAsync();

        var put = await PutAsync(superAdmin, new { businessHours = WorkWeek(), timeZone = "UTC", ticketPrefix = "sup" });
        var text = await (await superAdmin.GetAsync("/api/settings")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Contains("\"enabled\":true", text);
        Assert.Contains("\"timeZone\":\"UTC\"", text);
        Assert.Contains("\"ticketPrefix\":\"SUP-\"", text);

        await PutAsync(superAdmin, new { businessHours = WorkWeek(false), timeZone = "Asia/Riyadh", ticketPrefix = "TKT-" });
    }

    [Fact]
    public async Task Secrets_AreStoredEncrypted_NeverReturned_AndAppliedToTheChannelOptions()
    {
        var superAdmin = await SuperAdminAsync();

        var put = await PutAsync(superAdmin, new
        {
            email = new { fromAddress = "support@azm.example", smtpHost = "smtp.azm.example", smtpPort = 587, smtpSecurity = "StartTls", imapPort = 993, imapSecurity = "SslOnConnect" },
            whatsApp = new { phoneNumberId = "555000111" },
            secrets = new { smtpPassword = SmtpPassword, whatsAppAccessToken = AccessToken },
        });
        var putText = await put.Content.ReadAsStringAsync();
        var getText = await (await superAdmin.GetAsync("/api/settings")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.DoesNotContain(AccessToken, putText + getText);
        Assert.DoesNotContain(SmtpPassword, putText + getText);
        Assert.Contains("\"smtpPassword\":true", getText);
        Assert.Contains("\"whatsAppAccessToken\":true", getText);
        Assert.Contains("\"whatsAppAppSecret\":false", getText);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var stored = await db.SystemSettings.AsNoTracking().Where(s => s.IsSecret && s.Value != null).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, s => Assert.DoesNotContain("secret", s.Value!, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stored, s => s.Value == AccessToken || s.Value == SmtpPassword);

        // The stored credentials are the live ones now.
        var whatsApp = factory.Services.GetRequiredService<WhatsAppChannelOptions>();
        var email = factory.Services.GetRequiredService<EmailChannelOptions>();
        Assert.Equal("555000111", whatsApp.PhoneNumberId);
        Assert.Equal(AccessToken, whatsApp.AccessToken);
        Assert.Equal("smtp.azm.example", email.Smtp.Host);
        Assert.Equal(SmtpPassword, email.Smtp.Password);
        Assert.Equal(CrmApiFactory.WhatsAppVerifyToken, whatsApp.VerifyToken); // not stored: the configuration stays the fallback

        // The audit entry says that secrets changed, never their values.
        var audit = await (await superAdmin.GetAsync("/api/audit-logs?action=settings.updated&pageSize=100")).Content.ReadAsStringAsync();
        Assert.Contains("secretsChanged", audit);
        Assert.DoesNotContain(AccessToken, audit);
        Assert.DoesNotContain(SmtpPassword, audit);

        // Clearing a secret falls back to the configuration (here: nothing).
        await PutAsync(superAdmin, new { secrets = new { smtpPassword = "", whatsAppAccessToken = "" } });
        Assert.Null(whatsApp.AccessToken);
        Assert.Null(email.Smtp.Password);
        Assert.Contains("\"smtpPassword\":false", await (await superAdmin.GetAsync("/api/settings")).Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("""{"businessHours":{"enabled":true,"days":[],"start":"08:00","end":"16:00"}}""", "businessHours.days")]
    [InlineData("""{"businessHours":{"enabled":true,"days":["monday"],"start":"17:00","end":"16:00"}}""", "businessHours.end")]
    [InlineData("""{"businessHours":{"enabled":true,"days":["monday"],"start":"8am","end":"16:00"}}""", "businessHours.start")]
    [InlineData("""{"timeZone":"Mars/Olympus"}""", "timeZone")]
    [InlineData("""{"ticketPrefix":"A B"}""", "ticketPrefix")]
    [InlineData("""{"email":{"smtpPort":70000}}""", "email.smtpPort")]
    [InlineData("""{"email":{"fromAddress":"nope"}}""", "email.fromAddress")]
    public async Task InvalidValues_Return400_WithTheField(string json, string field)
    {
        var response = await (await SuperAdminAsync()).PutAsync("/api/settings", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Supervisor")]
    [InlineData("Agent")]
    public async Task EveryoneButSuperAdmin_Gets403_OnGetAndPut(string role)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(client, new { ticketPrefix = "HACK-" })).StatusCode);
        var superAdmin = await SuperAdminAsync();
        Assert.DoesNotContain("HACK-", await (await superAdmin.GetAsync("/api/settings")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutAToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task BusinessHoursAndPrefix_ApplyToNewTickets_ButNotToExistingOnes()
    {
        // Thursday 15:00 UTC: the clock is set before anybody signs in, so the tokens match it.
        factory.Time.SetUtcNow(new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero));
        var superAdmin = await SuperAdminAsync();
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        await PutAsync(superAdmin, new { businessHours = WorkWeek(false), timeZone = "UTC", ticketPrefix = "TKT-" });
        var before = await TicketArrange.TicketAsync(agent, customerId, priority: "high");
        var policy = (await superAdmin.GetFromJsonAsync<SlaPolicyBody[]>("/api/sla-policies"))!.Single(p => p.Priority == "high");

        await PutAsync(superAdmin, new { businessHours = WorkWeek(), timeZone = "UTC", ticketPrefix = "SUP-" });
        var after = await TicketArrange.TicketAsync(agent, customerId, priority: "high");

        var beforeSla = (await agent.GetFromJsonAsync<TicketSlaBody>($"/api/tickets/{before.Id}"))!;
        var afterSla = (await agent.GetFromJsonAsync<TicketSlaBody>($"/api/tickets/{after.Id}"))!;
        Assert.StartsWith("TKT-", before.Number);
        Assert.StartsWith("SUP-", after.Number);
        Assert.Equal(beforeSla.CreatedAt.AddMinutes(policy.ResponseMinutes), beforeSla.ResponseDueAt); // 24/7, unchanged
        // Thursday 15:00: 60 business minutes left, Friday / Saturday closed → Sunday 08:00 + (response - 60).
        var expected = new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc).AddMinutes(policy.ResponseMinutes - 60);
        Assert.Equal(expected, afterSla.ResponseDueAt);
        Assert.Equal(afterSla.CreatedAt, new DateTime(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc));

        await PutAsync(superAdmin, new { businessHours = WorkWeek(false), timeZone = "Asia/Riyadh", ticketPrefix = "TKT-" });
    }

    [Fact]
    public async Task SearchingByNumber_WorksWithTheConfiguredPrefix()
    {
        var superAdmin = await SuperAdminAsync();
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        await PutAsync(superAdmin, new { ticketPrefix = "ZZ-" });
        var ticket = await TicketArrange.TicketAsync(agent, customerId, priority: "mid");

        var found = await agent.GetStringAsync($"/api/tickets?search={Uri.EscapeDataString(ticket.Number)}");

        Assert.Contains(ticket.Id.ToString(), found);
        await PutAsync(superAdmin, new { ticketPrefix = "TKT-" });
    }
}
