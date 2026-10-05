using System.Net;
using Crm.Api.IntegrationTests.Infrastructure;

namespace Crm.Api.IntegrationTests.Auth;

/// <summary>Own class (own factory) because it moves the fake clock.</summary>
public class TokenExpiryTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_Returns401()
    {
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        factory.Time.Advance(TimeSpan.FromMinutes(61));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
