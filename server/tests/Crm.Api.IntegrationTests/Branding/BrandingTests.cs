using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Branding;

/// <summary>CRM-63: branding endpoints (colors and logo) and who may change them.</summary>
public class BrandingTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record BrandingBody(string? PrimaryColor, string? SecondaryColor, string? LogoUrl);

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static MultipartFormDataContent File(string name, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", name);
        return content;
    }

    private async Task<HttpClient> SuperAdminAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    [Fact]
    public async Task Anonymous_CanReadTheBranding_AndWithoutAnyItIsTheDefault()
    {
        var superAdmin = await SuperAdminAsync();
        await superAdmin.PutAsJsonAsync("/api/branding", new { primaryColor = "", secondaryColor = "" });
        await superAdmin.DeleteAsync("/api/branding/logo");

        var response = await factory.CreateClient().GetAsync("/api/branding");
        var body = await response.Content.ReadFromJsonAsync<BrandingBody>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new BrandingBody(null, null, null), body);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/api/branding/logo")).StatusCode);
    }

    [Fact]
    public async Task SuperAdminSetsTheColors_EveryoneSeesThem()
    {
        var superAdmin = await SuperAdminAsync();

        var put = await superAdmin.PutAsJsonAsync("/api/branding", new { primaryColor = "#0A5CAD", secondaryColor = "#fff" });
        var anonymous = await factory.CreateClient().GetFromJsonAsync<BrandingBody>("/api/branding");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(("#0a5cad", "#fff"), (anonymous!.PrimaryColor, anonymous.SecondaryColor));
    }

    [Fact]
    public async Task AnInvalidColor_Returns400_OnTheField()
    {
        var superAdmin = await SuperAdminAsync();

        var response = await superAdmin.PutAsJsonAsync("/api/branding", new { primaryColor = "blue", secondaryColor = "#12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["primaryColor", "secondaryColor"], problem!.Errors.Keys.Order());
    }

    [Fact]
    public async Task TheLogo_RoundTrips_WithSafeHeaders_AndCanBeRemoved()
    {
        var superAdmin = await SuperAdminAsync();

        var upload = await superAdmin.PutAsync("/api/branding/logo", File("logo.png", Png));
        var branding = await upload.Content.ReadFromJsonAsync<BrandingBody>();
        var download = await factory.CreateClient().GetAsync(branding!.LogoUrl);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        var removed = await superAdmin.DeleteAsync("/api/branding/logo");
        var after = await factory.CreateClient().GetFromJsonAsync<BrandingBody>("/api/branding");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.StartsWith("/api/branding/logo?v=", branding.LogoUrl);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, bytes);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'none'", download.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Null(after!.LogoUrl);
    }

    [Fact]
    public async Task ALogoOverTwoMegabytes_Returns400_AndExactlyTwoMegabytesIsAccepted()
    {
        var superAdmin = await SuperAdminAsync();
        var exact = new byte[2 * 1024 * 1024];
        Png.CopyTo(exact, 0);
        var over = new byte[exact.Length + 1];
        Png.CopyTo(over, 0);

        var tooBig = await superAdmin.PutAsync("/api/branding/logo", File("logo.png", over));
        var ok = await superAdmin.PutAsync("/api/branding/logo", File("logo.png", exact));

        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.Equal(["file"], (await tooBig.Content.ReadFromJsonAsync<HttpValidationProblemDetails>())!.Errors.Keys);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task AWrongTypeOrMissingFile_Returns400()
    {
        var superAdmin = await SuperAdminAsync();

        var svg = await superAdmin.PutAsync("/api/branding/logo", File("logo.svg", "<svg/>"u8.ToArray()));
        var fake = await superAdmin.PutAsync("/api/branding/logo", File("logo.png", "not an image"u8.ToArray()));
        var missing = await superAdmin.PutAsync("/api/branding/logo", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, svg.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task OnlySuperAdmin_MayChangeBranding_AnonymousGets401()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync("/api/branding", new { primaryColor = "#fff" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsync("/api/branding/logo", File("logo.png", Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.DeleteAsync("/api/branding/logo")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/branding", new { primaryColor = "#fff" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsync("/api/branding/logo", File("logo.png", Png))).StatusCode);
    }

    [Fact]
    public async Task ChangingTheBranding_IsInTheAuditLog()
    {
        var superAdmin = await SuperAdminAsync();
        await superAdmin.PutAsJsonAsync("/api/branding", new { primaryColor = "#123456" });

        var audit = await (await superAdmin.GetAsync("/api/audit-logs?action=branding.updated&pageSize=100")).Content.ReadAsStringAsync();

        Assert.Contains("branding.updated", audit);
        Assert.Contains("#123456", audit);
    }
}
