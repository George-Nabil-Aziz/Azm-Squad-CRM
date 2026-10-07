using System.Net;
using System.Net.Http.Json;
using Crm.Api.Auth;
using Crm.Api.IntegrationTests.Infrastructure;

namespace Crm.Api.IntegrationTests.Audit;

/// <summary>The audit log records the real client address: IPv4-mapped IPv6 becomes IPv4, X-Forwarded-For from a trusted (loopback) proxy is honoured.</summary>
public class ClientIpTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<string?> LatestLoginIpAsync(string email)
    {
        var admin = factory.CreateAuthenticatedClient(await factory.LoginAsync());
        var page = (await admin.GetFromJsonAsync<AuditLogPageBody>("/api/audit-logs?action=login.succeeded&pageSize=100"))!;
        return page.Items.Where(e => e.UserEmail == email).OrderByDescending(e => e.Id).First().IpAddress;
    }

    private async Task<string?> LoginWithHeaderAsync(string? forwardedFor)
    {
        var email = $"ip-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, "Agent");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = CrmApiFactory.TestUserPassword }),
        };
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await LatestLoginIpAsync(email);
    }

    [Fact]
    public async Task Login_WithoutProxyHeader_RecordsTheConnectionAddress()
    {
        Assert.Equal("127.0.0.1", await LoginWithHeaderAsync(null));
    }

    [Fact]
    public async Task Login_ThroughALoopbackProxy_RecordsTheForwardedClientAddress()
    {
        Assert.Equal("203.0.113.7", await LoginWithHeaderAsync("203.0.113.7"));
    }

    [Fact]
    public async Task Login_WithAForwardedIpv4MappedAddress_RecordsPlainIpv4()
    {
        Assert.Equal("198.51.100.4", await LoginWithHeaderAsync("::ffff:198.51.100.4"));
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1", "127.0.0.1")]
    [InlineData("::1", "::1")]
    [InlineData("192.168.1.20", "192.168.1.20")]
    [InlineData("2001:db8::5", "2001:db8::5")]
    public void Normalize_MapsIpv4MappedIpv6ToIpv4(string input, string expected) =>
        Assert.Equal(expected, ClientIp.Normalize(IPAddress.Parse(input)));

    [Fact]
    public void Normalize_Null_ReturnsNull() => Assert.Null(ClientIp.Normalize(null));
}
