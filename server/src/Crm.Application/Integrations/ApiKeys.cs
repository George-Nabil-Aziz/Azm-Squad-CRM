using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Domain.Integrations;
using FluentValidation;

namespace Crm.Application.Integrations;

/// <summary>Body of POST /api/api-keys: a name and at least one scope (<see cref="ApiKeyScopes"/>).</summary>
public sealed record CreateApiKeyRequest(string? Name, IReadOnlyList<string>? Scopes);

/// <summary>An API key as listed: never contains the key itself.</summary>
public sealed record ApiKeyResponse(
    Guid Id, string Name, string KeyPrefix, IReadOnlyList<string> Scopes, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? RevokedAt);

/// <summary>The response of creating a key: the only time <c>Key</c> is returned.</summary>
public sealed record CreatedApiKeyResponse(
    Guid Id, string Name, string KeyPrefix, IReadOnlyList<string> Scopes, DateTime CreatedAt, string Key);

/// <summary>API key storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface IApiKeyRepository
{
    void Add(ApiKey key);

    Task<ApiKey?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiKey?> FindByHashAsync(string hash, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// API keys. Admin use (<c>integrations.manage</c>, enforced by the API): create, list, revoke. Public API use:
/// <see cref="AuthenticateAsync"/> throws <c>UnauthorizedException</c> 401 (missing, unknown or revoked key) or
/// <c>ForbiddenException</c> 403 (the key lacks the scope).
/// </summary>
public interface IApiKeyService
{
    Task<CreatedApiKeyResponse> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiKeyResponse>> ListAsync(CancellationToken cancellationToken);

    Task RevokeAsync(Guid id, CancellationToken cancellationToken);

    Task<ApiKey> AuthenticateAsync(string? plainKey, string requiredScope, CancellationToken cancellationToken);
}

public sealed class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(_ => IntegrationText.NameRequired).MaximumLength(ApiKey.NameMaxLength);
        RuleFor(x => x.Scopes).NotEmpty().WithMessage(_ => IntegrationText.ScopesRequired);
        RuleForEach(x => x.Scopes).Must(ApiKeyScopes.IsKnown).WithMessage((_, scope) => IntegrationText.ScopeUnknown(scope));
    }
}

public sealed class ApiKeyService(
    IApiKeyRepository keys, ICurrentUser currentUser, TimeProvider timeProvider, IValidator<CreateApiKeyRequest> validator) : IApiKeyService
{
    public async Task<CreatedApiKeyResponse> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var (key, plain) = ApiKey.Generate(request.Name!, request.Scopes!, currentUser.UserId, timeProvider.GetUtcNow().UtcDateTime);
        keys.Add(key);
        await keys.SaveChangesAsync(cancellationToken);
        return new CreatedApiKeyResponse(key.Id, key.Name, key.KeyPrefix, key.ScopeList, key.CreatedAt, plain);
    }

    public async Task<IReadOnlyList<ApiKeyResponse>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await keys.ListAsync(cancellationToken)).Select(k => new ApiKeyResponse(
            k.Id, k.Name, k.KeyPrefix, k.ScopeList, k.CreatedAt, k.LastUsedAt, k.RevokedAt))];

    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = await keys.FindAsync(id, cancellationToken) ?? throw new NotFoundException(IntegrationText.KeyNotFound);
        key.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await keys.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApiKey> AuthenticateAsync(string? plainKey, string requiredScope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plainKey) || !plainKey.StartsWith(ApiKey.Prefix, StringComparison.Ordinal))
        {
            throw new UnauthorizedException(IntegrationText.KeyInvalid);
        }

        var key = await keys.FindByHashAsync(ApiKey.Hash(plainKey), cancellationToken);
        if (key is null || key.IsRevoked)
        {
            throw new UnauthorizedException(IntegrationText.KeyInvalid);
        }

        if (!key.HasScope(requiredScope))
        {
            throw new ForbiddenException(IntegrationText.ScopeMissing(requiredScope));
        }

        key.MarkUsed(timeProvider.GetUtcNow().UtcDateTime);
        await keys.SaveChangesAsync(cancellationToken);
        return key;
    }
}

/// <summary>User-facing text of the integrations feature, in the request language.</summary>
public static class IntegrationText
{
    public static string NameRequired => LocalizedText.Get("Enter a name.", "أدخل اسماً.");

    public static string ScopesRequired => LocalizedText.Get("Choose at least one scope.", "اختر نطاقاً واحداً على الأقل.");

    public static string ScopeUnknown(string scope) => LocalizedText.Get($"Unknown scope \"{scope}\".", $"نطاق غير معروف \"{scope}\".");

    public static string KeyNotFound => LocalizedText.Get("The API key was not found.", "مفتاح API غير موجود.");

    public static string KeyInvalid => LocalizedText.Get(
        "A valid API key is required (header X-Api-Key).", "مطلوب مفتاح API صالح (الترويسة X-Api-Key).");

    public static string ScopeMissing(string scope) => LocalizedText.Get(
        $"This API key does not have the scope \"{scope}\".", $"مفتاح API هذا لا يملك النطاق \"{scope}\".");
}
