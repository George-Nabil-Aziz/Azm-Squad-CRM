using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Portal;

public class PortalLoginTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private PortalApp Portal => PortalApp.Create(factory);

    [Fact]
    public async Task EnteringAnEmail_MailsACode_AndTheCorrectCodeSignsTheCustomerIn()
    {
        var portal = Portal;
        var email = PortalApp.NewEmail();

        await portal.RequestCodeAsync(email);
        var code = portal.Email.LastCodeFor(email);
        var response = await portal.VerifyAsync(email, code);
        var login = await response.Content.ReadFromJsonAsync<PortalLoginBody>();

        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", login!.TokenType);
        Assert.NotEmpty(login.AccessToken);
        Assert.Equal(email, login.Customer.Email);
        var me = await portal.Authenticated(login.AccessToken).GetFromJsonAsync<PortalCustomerBody>("/api/portal/auth/me");
        Assert.Equal(login.Customer.Id, me!.Id);
    }

    [Fact]
    public async Task AWrongCode_Returns401_AndDoesNotSignIn()
    {
        var portal = Portal;
        var email = PortalApp.NewEmail();
        await portal.RequestCodeAsync(email);
        var wrong = portal.Email.LastCodeFor(email) == "000000" ? "111111" : "000000";

        var response = await portal.VerifyAsync(email, wrong);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ACode_ExpiresAfterTenMinutes()
    {
        var portal = Portal;
        var justInTime = PortalApp.NewEmail();
        var tooLate = PortalApp.NewEmail();
        await portal.RequestCodeAsync(justInTime);
        await portal.RequestCodeAsync(tooLate);

        factory.Time.Advance(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(59));
        var early = await portal.VerifyAsync(justInTime, portal.Email.LastCodeFor(justInTime));
        factory.Time.Advance(TimeSpan.FromSeconds(1));
        var late = await portal.VerifyAsync(tooLate, portal.Email.LastCodeFor(tooLate));

        Assert.Equal(HttpStatusCode.OK, early.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, late.StatusCode);
    }

    [Fact]
    public async Task ACode_WorksOnlyOnce_AndANewCodeReplacesTheOldOne()
    {
        var portal = Portal;
        var email = PortalApp.NewEmail();
        await portal.RequestCodeAsync(email);
        var first = portal.Email.LastCodeFor(email);
        await portal.VerifyAsync(email, first);

        var again = await portal.VerifyAsync(email, first);

        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);

        var replaced = PortalApp.NewEmail();
        await portal.RequestCodeAsync(replaced);
        var oldCode = portal.Email.LastCodeFor(replaced);
        factory.Time.Advance(TimeSpan.FromMinutes(2));
        await portal.RequestCodeAsync(replaced);
        var newCode = portal.Email.LastCodeFor(replaced);

        if (oldCode != newCode)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await portal.VerifyAsync(replaced, oldCode)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await portal.VerifyAsync(replaced, newCode)).StatusCode);
    }

    [Fact]
    public async Task RequestingACodeTwiceInAMinute_SendsOnlyOneMail()
    {
        var portal = Portal;
        var email = PortalApp.NewEmail();

        await portal.RequestCodeAsync(email);
        await portal.RequestCodeAsync(email);

        Assert.Equal(1, portal.Email.CountTo(email));
    }

    [Fact]
    public async Task FiveWrongTries_BurnTheCode()
    {
        var portal = Portal;
        var email = PortalApp.NewEmail();
        await portal.RequestCodeAsync(email);
        var code = portal.Email.LastCodeFor(email);
        var wrong = code == "999999" ? "888888" : "999999";

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await portal.VerifyAsync(email, wrong)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await portal.VerifyAsync(email, code)).StatusCode);
    }

    [Fact]
    public async Task ThePortalAccount_IsLinkedToTheExistingCustomerWithTheSameEmail()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var email = PortalApp.NewEmail();
        var created = await staff.PostAsJsonAsync("/api/customers", new { name = "Nour Trading", email });
        var customerId = (await created.Content.ReadFromJsonAsync<PortalCustomerBody>())!.Id;

        var (_, customer) = await portal.SignInAsync(email);
        var customers = await staff.GetFromJsonAsync<JsonElement>($"/api/customers?search={Uri.EscapeDataString(email)}");

        Assert.Equal(customerId, customer.Id);
        Assert.Equal("Nour Trading", customer.Name);
        Assert.Equal(1, customers.GetProperty("totalCount").GetInt32()); // no duplicate customer was created
    }

    [Fact]
    public async Task AnUnknownEmail_CreatesTheCustomer_AtTheFirstSignIn()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var email = PortalApp.NewEmail();

        var (_, customer) = await portal.SignInAsync(email);
        var loaded = await staff.GetFromJsonAsync<JsonElement>($"/api/customers/{customer.Id}");
        factory.Time.Advance(TimeSpan.FromMinutes(2)); // a new code may be requested a minute after the last one
        var again = await portal.SignInAsync(email);

        Assert.Equal(email, loaded.GetProperty("email").GetString());
        Assert.Equal(customer.Id, again.Customer.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task AnInvalidEmail_Returns400(string email)
    {
        var response = await Portal.Anonymous().PostAsJsonAsync("/api/portal/auth/request-code", new { email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACustomerToken_CallingStaffApis_Returns403()
    {
        var portal = Portal;
        var (token, _) = await portal.SignInAsync();
        var customer = portal.Authenticated(token);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true
                        && !e.RoutePattern.RawText.StartsWith("/api/portal/", StringComparison.Ordinal)
                        && e.Metadata.GetMetadata<IAllowAnonymous>() is null
                        && e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy is not null))
            .ToList();

        Assert.True(endpoints.Count > 30);
        foreach (var endpoint in endpoints)
        {
            var method = new HttpMethod(endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.First());
            var path = Regex.Replace(endpoint.RoutePattern.RawText!, @"\{[^}]+\}", _ => Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(method, path);
            if (method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                request.Content = JsonContent.Create(new { });
            }

            var response = await customer.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
            {
                // Upload endpoints only match multipart requests (routing answers before authorization does).
                using var upload = new HttpRequestMessage(method, path) { Content = new MultipartFormDataContent { { new StringContent("x"), "file", "a.txt" } } };
                response = await customer.SendAsync(upload);
            }

            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {path} answered {(int)response.StatusCode}, expected 403");
        }
    }

    [Fact]
    public async Task AStaffToken_CallingThePortal_Returns403()
    {
        var staff = await Portal.StaffAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/portal/auth/me")).StatusCode);
    }

    [Fact]
    public async Task WithoutAToken_ThePortalMeEndpoint_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Portal.Anonymous().GetAsync("/api/portal/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ATokenOfADeletedCustomer_StopsWorking()
    {
        var portal = Portal;
        var staff = await portal.StaffAsync();
        var (token, customer) = await portal.SignInAsync();

        await staff.DeleteAsync($"/api/customers/{customer.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await portal.Authenticated(token).GetAsync("/api/portal/auth/me")).StatusCode);
    }

    [Fact]
    public async Task TheCustomerToken_CarriesTheCustomerRole_AndNoStaffPermissions()
    {
        var portal = Portal;
        var (token, customer) = await portal.SignInAsync();

        var me = await portal.Authenticated(token).GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.Equal(customer.Id, me.GetProperty("id").GetGuid());
        Assert.Equal(["Customer"], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Empty(me.GetProperty("permissions").EnumerateArray());
    }
}
