using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Sla;

public class SlaPoliciesTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<HttpClient> SuperAdminAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    [Fact]
    public async Task DefaultPolicies_AreSeeded_ForHighMidLow()
    {
        var superAdmin = await SuperAdminAsync();

        var policies = await superAdmin.GetFromJsonAsync<SlaPolicyBody[]>("/api/sla-policies");

        Assert.Equal(["high", "mid", "low"], policies!.Select(p => p.Priority));
        Assert.All(policies!, p => Assert.True(p.ResponseMinutes > 0 && p.ResolutionMinutes >= p.ResponseMinutes));
    }

    [Fact]
    public async Task SuperAdmin_UpdatesHigh_To1hAnd4h_ValuesAreSaved()
    {
        var superAdmin = await SuperAdminAsync();
        var before = (await superAdmin.GetFromJsonAsync<SlaPolicyBody[]>("/api/sla-policies"))!.Single(p => p.Priority == "high");

        var response = await superAdmin.PutAsJsonAsync("/api/sla-policies/high", new { responseMinutes = 60, resolutionMinutes = 240 });
        var saved = await response.Content.ReadFromJsonAsync<SlaPolicyBody>();
        var after = (await superAdmin.GetFromJsonAsync<SlaPolicyBody[]>("/api/sla-policies"))!.Single(p => p.Priority == "high");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((60, 240), (saved!.ResponseMinutes, saved.ResolutionMinutes));
        Assert.Equal((60, 240), (after.ResponseMinutes, after.ResolutionMinutes));
        Assert.Equal(DateTimeKind.Utc, after.UpdatedAt.Kind);

        // The database is shared by the tests of this class: put the seeded values back.
        await superAdmin.PutAsJsonAsync("/api/sla-policies/high", new { before.ResponseMinutes, before.ResolutionMinutes });
    }

    [Theory]
    [InlineData(0, 240, "responseMinutes")]
    [InlineData(60, -1, "resolutionMinutes")]
    public async Task Update_WithZeroOrNegative_Returns400_WithTheField(int response, int resolution, string field)
    {
        var superAdmin = await SuperAdminAsync();

        var result = await superAdmin.PutAsJsonAsync("/api/sla-policies/mid", new { responseMinutes = response, resolutionMinutes = resolution });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var problem = await result.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Update_WithResolutionBelowResponse_Returns400()
    {
        var superAdmin = await SuperAdminAsync();

        var result = await superAdmin.PutAsJsonAsync("/api/sla-policies/low", new { responseMinutes = 240, resolutionMinutes = 60 });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var problem = await result.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["resolutionMinutes"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task Update_WithEmptyBody_Returns400()
    {
        var superAdmin = await SuperAdminAsync();

        var result = await superAdmin.PutAsJsonAsync("/api/sla-policies/low", new { });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownPriority_Returns404()
    {
        var superAdmin = await SuperAdminAsync();

        var result = await superAdmin.PutAsJsonAsync("/api/sla-policies/urgent", new { responseMinutes = 60, resolutionMinutes = 240 });

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }
}
