using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Integrations;
using Crm.Domain.Integrations;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.UnitTests.Integrations;

public class ApiKeyTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Generate_ReturnsAKeyThatIsOnlyStoredAsAHash()
    {
        var (key, plain) = ApiKey.Generate("ERP bridge", [ApiKeyScopes.TicketsRead], null, Now);

        Assert.StartsWith("crm_", plain);
        Assert.True(plain.Length >= 40);
        Assert.Equal(ApiKey.Hash(plain), key.KeyHash);
        Assert.Equal(plain[..8], key.KeyPrefix);
        Assert.DoesNotContain(plain, key.KeyHash);
        Assert.NotEqual(plain, ApiKey.Generate("x", [ApiKeyScopes.TicketsRead], null, Now).PlainKey);
    }

    [Fact]
    public void Scopes_AreCheckedExactly_AndRevokeIsRemembered()
    {
        var (key, _) = ApiKey.Generate("k", [ApiKeyScopes.TicketsRead, ApiKeyScopes.CustomersRead], null, Now);

        Assert.True(key.HasScope(ApiKeyScopes.TicketsRead));
        Assert.False(key.HasScope(ApiKeyScopes.TicketsWrite));
        Assert.False(key.IsRevoked);
        key.Revoke(Now);
        Assert.True(key.IsRevoked);
        Assert.Equal(Now, key.RevokedAt);
    }

    [Fact]
    public void IsKnown_AcceptsOnlyTheFourScopes()
    {
        Assert.All(ApiKeyScopes.All, scope => Assert.True(ApiKeyScopes.IsKnown(scope)));
        Assert.False(ApiKeyScopes.IsKnown("tickets:delete"));
        Assert.Equal(4, ApiKeyScopes.All.Count);
    }
}

public class ApiKeyServiceTests
{
    private readonly FakeApiKeyRepository _repo = new();
    private readonly IntegrationClock _time = new(new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero));

    private ApiKeyService Service() => new(_repo, new NobodyUser(), _time, new CreateApiKeyRequestValidator());

    [Fact]
    public async Task Create_ShowsTheKeyOnce_AndStoresOnlyTheHash()
    {
        var created = await Service().CreateAsync(new CreateApiKeyRequest("Shop", [ApiKeyScopes.TicketsRead]), default);

        var stored = Assert.Single(_repo.Keys);
        Assert.StartsWith("crm_", created.Key);
        Assert.Equal(ApiKey.Hash(created.Key), stored.KeyHash);
        Assert.NotEqual(created.Key, stored.KeyHash);
        var listed = Assert.Single(await Service().ListAsync(default));
        Assert.Equal(created.KeyPrefix, listed.KeyPrefix); // the list type has no Key member at all
    }

    [Theory]
    [InlineData("", "tickets:read", "name")]
    [InlineData("Shop", "bogus", "scopes[0]")]
    public async Task Create_RejectsBadInput_WithFieldErrors(string name, string scope, string field)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new CreateApiKeyRequest(name, [scope]), default));

        Assert.Contains(field, error.Errors.Keys);
        Assert.Empty(_repo.Keys);
    }

    [Fact]
    public async Task Create_WithoutScopes_IsRejected() =>
        Assert.Contains("scopes", (await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateAsync(new CreateApiKeyRequest("Shop", []), default))).Errors.Keys);

    [Fact]
    public async Task Authenticate_AcceptsAValidKeyWithTheScope()
    {
        var created = await Service().CreateAsync(new CreateApiKeyRequest("Shop", [ApiKeyScopes.TicketsRead]), default);

        var key = await Service().AuthenticateAsync(created.Key, ApiKeyScopes.TicketsRead, default);

        Assert.Equal(created.Id, key.Id);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, _repo.Keys[0].LastUsedAt);
    }

    [Fact]
    public async Task Authenticate_WrongScope_IsForbidden()
    {
        var created = await Service().CreateAsync(new CreateApiKeyRequest("Shop", [ApiKeyScopes.TicketsRead]), default);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service().AuthenticateAsync(created.Key, ApiKeyScopes.TicketsWrite, default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("crm_unknownunknownunknownunknownunknownunknown")]
    public async Task Authenticate_MissingOrUnknownKey_IsUnauthorized(string? plain) =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service().AuthenticateAsync(plain, ApiKeyScopes.TicketsRead, default));

    [Fact]
    public async Task Authenticate_RevokedKey_IsUnauthorized()
    {
        var created = await Service().CreateAsync(new CreateApiKeyRequest("Shop", [ApiKeyScopes.TicketsRead]), default);
        await Service().RevokeAsync(created.Id, default);

        await Assert.ThrowsAsync<UnauthorizedException>(() => Service().AuthenticateAsync(created.Key, ApiKeyScopes.TicketsRead, default));
    }

    [Fact]
    public async Task Revoke_UnknownKey_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Service().RevokeAsync(Guid.NewGuid(), default));
}

internal sealed class NobodyUser : ICurrentUser
{
    public Guid? UserId => null;

    public bool IsInRole(string role) => false;

    public bool HasPermission(string permission) => false;
}

internal sealed class FakeApiKeyRepository : IApiKeyRepository
{
    public List<ApiKey> Keys { get; } = [];

    public void Add(ApiKey key) => Keys.Add(key);

    public Task<ApiKey?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Keys.FirstOrDefault(k => k.Id == id));

    public Task<ApiKey?> FindByHashAsync(string hash, CancellationToken cancellationToken) =>
        Task.FromResult(Keys.FirstOrDefault(k => k.KeyHash == hash));

    public Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ApiKey>>([.. Keys]);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A clock the test sets by hand.</summary>
internal sealed class IntegrationClock(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
