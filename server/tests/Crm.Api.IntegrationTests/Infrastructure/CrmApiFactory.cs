using System.Net.Http.Headers;
using System.Net.Http.Json;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Crm.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The one shared test host (CLAUDE.md "Integration tests"). Environment <c>Testing</c>.
/// SQLite in-memory database (one open connection for the factory lifetime, created with EnsureCreated
/// and seeded by the real startup code), test JWT key + seed password, a controllable clock in <see cref="Time"/>,
/// captured logs in <see cref="Logs"/> and test-only endpoints under <c>/_test</c>.
/// </summary>
public class CrmApiFactory : WebApplicationFactory<Program>
{
    public const string SuperAdminEmail = "admin@crm.local";
    public const string SuperAdminPassword = "Test#Admin123";
    public const string JwtSigningKey = "test-signing-key-for-integration-tests-only-0123456789";
    public const string TestUserPassword = "Test#User123";
    public const string WhatsAppVerifyToken = "test-verify-token";
    public const string SmsAuthToken = "test-sms-auth-token";
    public const string SmsWebhookBaseUrl = "https://crm.test";
    public const string WhatsAppAppSecret = "test-app-secret-for-integration-tests";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Root of the uploaded files of this factory (a temp folder, deleted on dispose; never inside the repository).</summary>
    public string FilesRoot { get; } = Path.Combine(Path.GetTempPath(), "crm-tests", Guid.NewGuid().ToString("N"));

    public TestLoggerProvider Logs { get; } = new();

    /// <summary>Clock used by the app (token issue time, expiry checks). Starts at the real current time.</summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            _connection.Open();
        }

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = JwtSigningKey,
            ["Seed:SuperAdminPassword"] = SuperAdminPassword,
            ["Database:StartupAction"] = "EnsureCreated",
            ["FileStorage:RootPath"] = FilesRoot,
            // Test-only webhook values (CRM-26); no access token, so sending through WhatsApp is "not configured".
            ["Channels:WhatsApp:VerifyToken"] = WhatsAppVerifyToken,
            ["Channels:WhatsApp:AppSecret"] = WhatsAppAppSecret,
            // Public endpoints share the loopback address in tests: a high limit, tests of the limit set their own.
            ["Channels:Sms:AuthToken"] = SmsAuthToken,
            ["Channels:Sms:WebhookBaseUrl"] = SmsWebhookBaseUrl,
            ["WebForms:RateLimitRequests"] = "1000",
        }));
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CrmDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CrmDbContext>>();
            services.AddDbContext<CrmDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            services.AddTransient<IStartupFilter, RemoteIpStartupFilter>();
            services.AddTransient<IStartupFilter, TestEndpointsStartupFilter>();
            services.AddScoped<FluentValidation.IValidator<SampleRequest>, SampleRequestValidator>();
        });
    }

    /// <summary>Logs in through the real endpoint and returns the access token.</summary>
    public async Task<string> LoginAsync(string email = SuperAdminEmail, string password = SuperAdminPassword)
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        return body!.AccessToken;
    }

    /// <summary>
    /// Creates an active user directly through <see cref="UserManager{TUser}"/> (not through the API) and returns its id.
    /// Use a unique email per test: the database is shared by all tests of a class.
    /// </summary>
    public async Task<Guid> CreateUserAsync(string email, string password, params string[] roles)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, FullName = email };
        var created = await users.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        var added = await users.AddToRolesAsync(user, roles);
        Assert.True(added.Succeeded, string.Join("; ", added.Errors.Select(e => e.Description)));
        return user.Id;
    }

    /// <summary>Creates a user with the given role (unique email) and returns a client signed in as that user.</summary>
    public async Task<HttpClient> CreateClientWithRoleAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@crm.local";
        await CreateUserAsync(email, TestUserPassword, role);
        return CreateAuthenticatedClient(await LoginAsync(email, TestUserPassword));
    }

    /// <summary>Client that sends <c>Authorization: Bearer &lt;token&gt;</c> on every request.</summary>
    public HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            if (Directory.Exists(FilesRoot))
            {
                Directory.Delete(FilesRoot, recursive: true);
            }
        }
    }

    public sealed record LoginBody(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);
}
