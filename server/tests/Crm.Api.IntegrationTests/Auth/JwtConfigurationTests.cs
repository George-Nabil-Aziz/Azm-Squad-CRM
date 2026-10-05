using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Crm.Api.IntegrationTests.Auth;

public class JwtConfigurationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public void Startup_WithTooShortSigningKey_Fails()
    {
        using var misconfigured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Jwt:SigningKey"] = "too-short" })));

        var exception = Assert.ThrowsAny<Exception>(() => misconfigured.CreateClient());

        var validation = Assert.IsType<OptionsValidationException>(exception.GetBaseException());
        Assert.Contains("SigningKey", validation.Message);
    }
}
