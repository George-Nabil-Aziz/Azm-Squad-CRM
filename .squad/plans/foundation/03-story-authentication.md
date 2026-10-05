# Story 03 — Authentication (login with JWT) (Story: CRM-2)

## Prerequisites

- Story 01 completed: [01-story-project-skeleton.md](01-story-project-skeleton.md) (CRM-1) — `server/` solution with `Crm.Api` → `Crm.Application` → `Crm.Domain` ← `Crm.Infrastructure`, `client/` app, `GET /api/health`. Merged to `main`.
- Story 02 completed: [02-story-global-error-handling.md](02-story-global-error-handling.md) (CRM-5) — `GlobalExceptionHandler` + ProblemDetails with `correlationId`, Application exceptions, `ValidateOrThrowAsync`, the shared `CrmApiFactory` test host, client `ApiError` / `onApiError` / `ApiErrorToaster`. Merged to `main`.
- Work on branch **`feature/crm-2-authentication`** (already created from `main`).
- Phase 1 order: CRM-1 ✅ → CRM-5 ✅ → **CRM-2 (this)** → CRM-3 (layout + shadcn/ui + styled login page) → CRM-4 (ar/en + RTL) → CRM-6 (users) → CRM-7 (roles & permissions).
- **SQL Server LocalDB** must be installed for the manual smoke test (`sqllocaldb info` lists `MSSQLLocalDB`). Automated tests do **not** need it (SQLite in-memory).
- **dotnet-ef 10.0.8** is installed globally. It prints "tools version '10.0.8' is older than that of the runtime '10.0.11'" — a harmless warning; the migration is generated correctly.
- **Shared contract created here** (every later story depends on it): `CrmDbContext` + first migration, `ApplicationUser` / `ApplicationRole`, the `Roles` constants and seed, `POST /api/auth/login` + `GET /api/auth/me` shapes, the JWT claim names (`sub`, `email`, `name`, `role`), the rule "every `/api/*` endpoint declares `RequireAuthorization()` or `AllowAnonymous()`" (guarded by a test), `CrmApiFactory.LoginAsync` / `CreateAuthenticatedClient` / `Time`, and the client `client/src/auth/` module.

---

## Story Goal

Staff users sign in with email + password and get a JWT access token; the API rejects every protected call without a valid, unexpired token.

1. `POST /api/auth/login` with valid email + password → **200** `{ accessToken, tokenType: "Bearer", expiresAt }` (AC 1).
2. Wrong password (or unknown email, or locked-out user) → **401** `application/problem+json`, detail "Invalid email or password.", **no token** (AC 2).
3. `GET /api/auth/me` (first protected endpoint) without a token → **401** ProblemDetails + `WWW-Authenticate: Bearer` (AC 3); with a valid token → **200** `{ id, email, fullName, roles }` (AC 4); with an expired token → **401** (AC 5).
4. Persistence foundation: `CrmDbContext` (ASP.NET Identity, `Guid` keys) + migration `InitialIdentity`; SQL Server LocalDB in Development (migrated at startup); roles `SuperAdmin`, `Admin`, `Supervisor`, `Agent` and the user `admin@crm.local` seeded at startup (password from `Seed:SuperAdminPassword`).
5. Client: token stored only by `client/src/auth/`, attached as `Authorization: Bearer …` by `client/src/api/client.ts`, a minimal unstyled sign-in form and a "Signed in as …" + "Sign out" panel on the home page.
6. **401 decision (left open by CRM-5):** the client never toasts a 401. A 401 from the login call is shown **inline** in the form ("Invalid email or password."); a 401 on any other call **clears the stored session**, so the app falls back to the sign-in form. All other failures keep toasting as in CRM-5.

**Not in scope:** styled login page, layout, routing / redirects (CRM-3); i18n / RTL (CRM-4); user management (CRM-6); permission policies (CRM-7); refresh tokens, remember-me, password reset, email confirmation, 2FA, external logins, rate limiting beyond Identity lockout.

---

## Context — Read These Files First

1. `CLAUDE.md` — **Architecture decisions** (binding): *Persistence* (`CrmDbContext` in `Crm.Infrastructure/Persistence/`, `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`, Identity types only in Infrastructure, migrations in `Crm.Infrastructure/Persistence/Migrations`), *Database* (LocalDB connection string `ConnectionStrings:Crm` in `appsettings.Development.json`, migrations at startup in Development only), *Integration tests* (one `CrmApiFactory`, `Testing`, SQLite in-memory with one open connection + `EnsureCreated`, config via `ConfigureAppConfiguration`, fake `TimeProvider`), *Secrets*, *Seed*, *Endpoints* (`Crm.Api/Endpoints/<Feature>Endpoints.cs`, groups under `/api/<resource>`), *Application layer*; frontend: token kept by `client/src/auth/`, attached by `client/src/api/client.ts`, feature components in `client/src/features/<feature>/`. **Backend rules**: authorization on the API, `TimeProvider`, async + `CancellationToken`. **Frontend rules**: no hard-coded hex colors, logical Tailwind classes, API calls only through `client/src/api`, test by role/label.
2. `.squad/stories/foundation/CRM-2/intake.md` — acceptance criteria 1–5 and **Out of scope**.
3. `server/src/Crm.Api/Program.cs` — whole file (25 lines). Lines 7–9 service registration, line 13 `app.UseCrmErrorHandling()` (**must stay the first middleware**; auth goes right after it), lines 15–18 Development-only `MapOpenApi()`, line 20 `MapHealthEndpoints()`, line 25 `public partial class Program;` (keep).
4. `server/src/Crm.Api/ErrorHandling/GlobalExceptionHandler.cs` — lines 50–86 `ToProblemDetails` switch. You add an `UnauthorizedException` arm **before** the `NotFoundException` arm at line 63. Lines 22–38: non-500 failures are logged as Warning with the correlation id (no change).
5. `server/src/Crm.Api/ErrorHandling/ErrorHandlingExtensions.cs` — lines 22–28: `UseStatusCodePages()` turns the empty 401 written by the JWT challenge into ProblemDetails (verified while planning — no extra code needed).
6. `server/src/Crm.Api/Endpoints/HealthEndpoints.cs` — lines 7–8: add `.AllowAnonymous()` (the endpoint-guard test requires every `/api/*` endpoint to declare it).
7. `server/src/Crm.Api/Crm.Api.csproj` — line 10 `Microsoft.AspNetCore.OpenApi` **10.0.11**: every new ASP.NET / EF package uses **10.0.11** too.
8. `server/src/Crm.Infrastructure/Crm.Infrastructure.csproj` — lines 3–6 references Application + Domain, no packages yet.
9. `server/src/Crm.Application/DependencyInjection.cs` — line 11 `AddValidatorsFromAssembly(...)` already registers every validator in the Application assembly, so the new `LoginRequestValidator` needs **no** registration.
10. `server/src/Crm.Application/Common/Exceptions/NotFoundException.cs` — one-line primary-constructor shape; `UnauthorizedException` copies it.
11. `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` — whole file (28 lines). Lines 18–27 `ConfigureWebHost`: keep the environment, `Logs`, `TestEndpointsStartupFilter` and `SampleRequest` validator registrations; you add SQLite, config, clock and two helpers.
12. `server/tests/Crm.Api.IntegrationTests/HealthEndpointTests.cs` — lines 7–20: unchanged, must stay green (proves `/api/health` is anonymous).
13. `server/tests/Crm.Api.IntegrationTests/ErrorHandling/ErrorHandlingTests.cs` — uses `factory.WithWebHostBuilder(b => b.UseEnvironment("Production" | "Development"))`; these derived hosts share the factory's SQLite connection and test config, and must stay green.
14. `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs` — lines 5–6 `ForbiddenPrefixes`; you add `"Microsoft.Extensions.Identity"` so Application can never use `UserManager` & co.
15. `server/tests/Crm.UnitTests/Common/ValidatorExtensionsTests.cs` — precedent for unit-test style (`[Fact]`, `Assert.*`).
16. `client/src/api/client.ts` — whole file (60 lines). Lines 33–56 private `request`; line 36 builds headers; lines 43–53 error path; lines 58–60 `apiGet`. You add the bearer header, 401 → `clearSession()`, `apiPost`, and 204 handling. **Keep** the message format `"<METHOD> <path> failed with status <n>"` (`client/src/api/health.test.ts` line 22 asserts `'503'`).
17. `client/src/components/ApiErrorToaster.tsx` — lines 12–18: listener; you add an early `return` for 401.
18. `client/src/App.tsx` — whole file (34 lines). The health `useEffect` (lines 10–18) stays; you render the sign-in form or the user panel inside `<main>` (lines 23–28).
19. `client/src/App.test.tsx` — line 6 mocks `./api/health`; signed out it now also renders the form; its two tests stay green unchanged. `client/src/App.toast.test.tsx` — real client with a stubbed `fetch`; stays green.
20. `client/src/test/setup.ts` — lines 5–7 `afterEach(cleanup)`; you add `localStorage.clear()` so a stored session never leaks between tests.
21. `client/tsconfig.app.json` — line 22 `"erasableSyntaxOnly": true` (no enums / parameter properties). `client/.oxlintrc.json` line 6 `react/only-export-components`: `.tsx` files export only components; hooks and helpers live in `.ts` files.
22. `.claude/skills/vercel-react-best-practices/SKILL.md` — frontend rules to follow (no barrel files: import modules directly).

Verified while planning (scratch clone of `main` in the session scratchpad — every snippet below compiled; **`dotnet test` 40 passed, `npm test` 25 passed, `npm run build` and `npm run lint` clean**; the migration was generated with the exact command below, applied to a throw-away LocalDB database by `dotnet run`, and login → `/api/auth/me` worked over HTTP):

- NuGet (all on nuget.org, ASP.NET/EF kept on **10.0.11** like `Microsoft.AspNetCore.OpenApi`): `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.11 (depends on `Microsoft.Extensions.Identity.Stores` + EF Relational, **no** ASP.NET framework reference — fine for Infrastructure), `Microsoft.EntityFrameworkCore.SqlServer` 10.0.11, `Microsoft.EntityFrameworkCore.Design` 10.0.11 (startup project), `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 (brings `Microsoft.IdentityModel.JsonWebTokens` 8.19.2 transitively — **do not** add IdentityModel packages directly, mixed IdentityModel versions break validation), `Microsoft.EntityFrameworkCore.Sqlite` 10.0.11 (tests), `Microsoft.Extensions.TimeProvider.Testing` **10.10.0** (latest; this package has its own version line, `FakeTimeProvider` in namespace `Microsoft.Extensions.Time.Testing`).
- **JwtBearer does not use the DI `TimeProvider` for token lifetime**: with only a fake clock, the expired-token test got **200**. `TokenValidationParameters.TimeProvider` is not public in IdentityModel 8.19.2 (compile error CS0117). The plan therefore sets `TokenValidationParameters.LifetimeValidator` to a check that reads the injected `TimeProvider` — with it, the expiry test is green.
- EF Core 10 registers `IDbContextOptionsConfiguration<CrmDbContext>` in addition to `DbContextOptions<CrmDbContext>`; the factory must remove **both** before adding SQLite, otherwise two providers are configured.
- `dotnet ef` builds the host but stops at `builder.Build()`, so startup migration/seed code and `ValidateOnStart` do not run at design time. It **does** need `ConnectionStrings:Crm` in `appsettings.Development.json` (escaped as `(localdb)\\MSSQLLocalDB` in JSON — a single backslash makes the file unreadable: "Failed to load configuration from file").
- The JWT challenge writes an empty 401; CRM-5's `UseStatusCodePages()` turns it into `application/problem+json` with `correlationId`, and the response keeps `WWW-Authenticate: Bearer`.
- sonner keeps toasts in a module-level store across tests in one file, so "no toast" assertions check for a **specific** text (`Reference: corr-401`), not the number of `listitem`s.

---

## Backend Tasks

All commands run from `server/` unless stated.

### 1 — Packages and user-secrets

```bash
dotnet add src/Crm.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 10.0.11
dotnet add src/Crm.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.11
dotnet add src/Crm.Api package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.11
dotnet add src/Crm.Api package Microsoft.EntityFrameworkCore.Design --version 10.0.11
dotnet add tests/Crm.Api.IntegrationTests package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.11
dotnet add tests/Crm.Api.IntegrationTests package Microsoft.Extensions.TimeProvider.Testing --version 10.10.0
dotnet user-secrets init --project src/Crm.Api
```

`dotnet add … Microsoft.EntityFrameworkCore.Design` writes `<IncludeAssets>…</IncludeAssets><PrivateAssets>all</PrivateAssets>` — keep it. `user-secrets init` adds a `<UserSecretsId>` GUID to `Crm.Api.csproj` — commit it (it is an id, not a secret). **Do not** add any package to `Crm.Application` or `Crm.Domain`.

### 2 — Unit tests first (Red)

**File: `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs`** — replace lines 5–6 with:

```csharp
    private static readonly string[] ForbiddenPrefixes =
        [
            "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.Identity",
            "Crm.Infrastructure", "Crm.Api",
        ];
```

**Create file: `server/tests/Crm.UnitTests/Auth/LoginRequestValidatorTests.cs`**

```csharp
using Crm.Application.Auth;

namespace Crm.UnitTests.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        var result = _validator.Validate(new LoginRequest("admin@crm.local", "Secret#123"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null, "Secret#123", "Email")]
    [InlineData("", "Secret#123", "Email")]
    [InlineData("not-an-email", "Secret#123", "Email")]
    [InlineData("admin@crm.local", null, "Password")]
    [InlineData("admin@crm.local", "", "Password")]
    public void InvalidRequest_ReportsTheField(string? email, string? password, string field)
    {
        var result = _validator.Validate(new LoginRequest(email, password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }
}
```

Run `dotnet test` → **Red** (compile error: `Crm.Application.Auth` does not exist).

### 3 — Application: contracts, validator, exception (Green for unit tests)

**Create file: `server/src/Crm.Application/Common/Exceptions/UnauthorizedException.cs`**

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>Caller could not be authenticated (e.g. wrong email or password). Mapped to 401 ProblemDetails.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);
```

**Create file: `server/src/Crm.Application/Auth/Roles.cs`**

```csharp
namespace Crm.Application.Auth;

/// <summary>Role names seeded at startup (CLAUDE.md "Seed"). CRM-7 builds permissions on top of them.</summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Supervisor = "Supervisor";
    public const string Agent = "Agent";

    public static IReadOnlyList<string> All { get; } = [SuperAdmin, Admin, Supervisor, Agent];
}
```

**Create file: `server/src/Crm.Application/Auth/AuthContracts.cs`**

```csharp
namespace Crm.Application.Auth;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

public sealed record CurrentUserResponse(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record AccessTokenSubject(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
```

`LoginRequest` properties are **nullable** on purpose: a missing field must reach the validator (400 with field errors), not fail JSON binding.

**Create file: `server/src/Crm.Application/Auth/LoginRequestValidator.cs`**

```csharp
using FluentValidation;

namespace Crm.Application.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
```

**Create file: `server/src/Crm.Application/Auth/IAuthService.cs`**

```csharp
namespace Crm.Application.Auth;

public interface IAuthService
{
    /// <summary>
    /// Checks email + password and returns a signed access token.
    /// Throws <c>ValidationException</c> (400) for an invalid request and
    /// <c>UnauthorizedException</c> (401) for unknown email, wrong password or a locked-out user.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

/// <summary>Creates signed access tokens. Implemented in Crm.Api (JWT); Application does not know the format.</summary>
public interface IAccessTokenGenerator
{
    AccessToken Generate(AccessTokenSubject subject);
}
```

Why the split: `IAuthService` is implemented in **Infrastructure** (needs `UserManager<ApplicationUser>`), `IAccessTokenGenerator` in **Api** (the JWT bearer package already lives there; adding IdentityModel to Infrastructure would risk mixed IdentityModel versions).

Run `dotnet test` → unit tests **Green** (**12 passed**: 2 architecture, 4 validator-extension, 6 login-validator). Integration tests still pass (14).

### 4 — Integration test host + integration tests first (Red)

**File: `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs`** — replace the whole file:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
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

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

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
        }));
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CrmDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CrmDbContext>>();
            services.AddDbContext<CrmDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

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
        }
    }

    public sealed record LoginBody(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);
}
```

The test signing key and password are **test-only constants** in the test project — not secrets, never used outside tests.

**Create file: `server/tests/Crm.Api.IntegrationTests/Auth/LoginTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Crm.Api.IntegrationTests.Auth;

public class LoginTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string LoginPath = "/api/auth/login";
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithAccessToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath,
            new { email = CrmApiFactory.SuperAdminEmail, password = CrmApiFactory.SuperAdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CrmApiFactory.LoginBody>();
        Assert.NotNull(body);
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(factory.Time.GetUtcNow().AddMinutes(60), body.ExpiresAt, TimeSpan.FromSeconds(1));

        var token = new JsonWebToken(body.AccessToken);
        Assert.Equal("Crm.Api", token.Issuer);
        Assert.Equal(["Crm.Client"], token.Audiences);
        Assert.Equal(CrmApiFactory.SuperAdminEmail, token.GetClaim("email").Value);
        Assert.True(Guid.TryParse(token.Subject, out _));
        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == "SuperAdmin");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithoutToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath,
            new { email = CrmApiFactory.SuperAdminEmail, password = "Wrong#Password1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem!.Status);
        Assert.Equal("Invalid email or password.", problem.Detail);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401WithSameMessage()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath,
            new { email = "nobody@crm.local", password = CrmApiFactory.SuperAdminPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", problem!.Detail);
    }

    [Fact]
    public async Task Login_WithMissingFields_Returns400WithFieldErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath, new { email = "not-an-email", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("email", problem!.Errors.Keys);
        Assert.Contains("password", problem.Errors.Keys);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_IsLockedOutEvenWithCorrectPassword()
    {
        const string email = "lockout@crm.local";
        const string password = "Lockout#123";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await users.CreateAsync(
                new ApplicationUser { UserName = email, Email = email, FullName = "Lockout User" }, password);
            Assert.True(result.Succeeded);
        }

        var client = factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsJsonAsync(LoginPath, new { email, password = "Wrong#Password1" });
        }

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

The lockout test uses its **own** user so the shared SuperAdmin is never locked for the other tests of the class.

**Create file: `server/tests/Crm.Api.IntegrationTests/Auth/ProtectedEndpointTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.IntegrationTests.Auth;

public class ProtectedEndpointTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string MePath = "/api/auth/me";

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401ProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200WithCurrentUser()
    {
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync());

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeBody>();
        Assert.Equal(CrmApiFactory.SuperAdminEmail, me!.Email);
        Assert.Equal("System Administrator", me.FullName);
        Assert.Equal(["SuperAdmin"], me.Roles);
        Assert.NotEqual(Guid.Empty, me.Id);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTokenSignedByAnotherKey_Returns401()
    {
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "Crm.Api",
            Audience = "Crm.Client",
            Expires = DateTime.UtcNow.AddMinutes(30),
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = "SuperAdmin" },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-key-that-is-long-enough-0123456789")),
                SecurityAlgorithms.HmacSha256),
        });
        var client = factory.CreateAuthenticatedClient(forged);

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMalformedAuthorizationHeader_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(endpoints);
        var unprotected = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null
                        && e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => e.RoutePattern.RawText)
            .ToList();
        Assert.Empty(unprotected);
    }

    private sealed record MeBody(Guid Id, string Email, string FullName, string[] Roles);
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Auth/TokenExpiryTests.cs`** — its own class (own factory) because it moves the clock.

```csharp
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
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Auth/SeedTests.cs`**

```csharp
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Auth;

public class SeedTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public async Task Startup_SeedsRolesAndSuperAdmin()
    {
        using var scope = factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roleNames = await roles.Roles.Select(r => r.Name!).ToListAsync();
        Assert.Equal(["Admin", "Agent", "SuperAdmin", "Supervisor"], roleNames.Order(StringComparer.Ordinal));
        var admin = await users.FindByEmailAsync(CrmApiFactory.SuperAdminEmail);
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(admin, "SuperAdmin"));
    }

    [Fact]
    public async Task Initializer_RunTwice_DoesNotDuplicateData()
    {
        using var scope = factory.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<CrmDbInitializer>();

        await initializer.InitializeAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        Assert.Equal(4, await db.Roles.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == CrmApiFactory.SuperAdminEmail));
    }
}
```

(Ordinal order: `"SuperAdmin"` sorts before `"Supervisor"` because `'A'` < `'v'`.)

**Create file: `server/tests/Crm.Api.IntegrationTests/Auth/JwtConfigurationTests.cs`**

```csharp
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
```

Run `dotnet test` → **Red** (compile errors: `Crm.Infrastructure.Persistence`, `Crm.Infrastructure.Identity`, `CrmDbInitializer` do not exist).

### 5 — Infrastructure: Identity, DbContext, auth service, seed

**Create file: `server/src/Crm.Infrastructure/Identity/ApplicationUser.cs`**

```csharp
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

/// <summary>Staff user (ASP.NET Identity). Lives in Infrastructure, never in Domain (CLAUDE.md).</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
}
```

**Create file: `server/src/Crm.Infrastructure/Identity/ApplicationRole.cs`**

```csharp
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}
```

**Create file: `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`**

```csharp
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        });
    }
}
```

**Create file: `server/src/Crm.Infrastructure/Identity/AuthService.cs`**

```csharp
using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IAccessTokenGenerator tokenGenerator,
    IValidator<LoginRequest> validator) : IAuthService
{
    // One message for unknown email, wrong password and locked-out user: never reveal which one it was.
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);

        var user = await userManager.FindByEmailAsync(request.Email!);
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password!))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var token = tokenGenerator.Generate(
            new AccessTokenSubject(user.Id, user.Email!, user.FullName, [.. roles]));

        return new LoginResponse(token.Token, "Bearer", token.ExpiresAt);
    }
}
```

(`UserManager` methods take no `CancellationToken`; the token is passed to the validator. That is the Identity API, not an omission.)

**Create file: `server/src/Crm.Infrastructure/Persistence/CrmDbInitializer.cs`**

```csharp
using Crm.Application.Auth;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence;

/// <summary>
/// Runs once at startup: prepares the database according to <c>Database:StartupAction</c>
/// (<c>Migrate</c> | <c>EnsureCreated</c> | anything else = nothing) and seeds the four roles and the
/// SuperAdmin user. Idempotent: safe to run on every start.
/// </summary>
public sealed class CrmDbInitializer(
    CrmDbContext db,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<CrmDbInitializer> logger)
{
    public const string SuperAdminEmail = "admin@crm.local";
    public const string SuperAdminFullName = "System Administrator";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        switch (configuration["Database:StartupAction"])
        {
            case "Migrate":
                await db.Database.MigrateAsync(cancellationToken);
                break;
            case "EnsureCreated":
                await db.Database.EnsureCreatedAsync(cancellationToken);
                break;
        }

        await SeedRolesAsync();
        await SeedSuperAdminAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                ThrowIfFailed(await roleManager.CreateAsync(new ApplicationRole(role)), $"create role '{role}'");
            }
        }
    }

    private async Task SeedSuperAdminAsync()
    {
        if (await userManager.FindByEmailAsync(SuperAdminEmail) is not null)
        {
            return;
        }

        var password = configuration["Seed:SuperAdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Seed:SuperAdminPassword is not configured; SuperAdmin user {Email} was not created.", SuperAdminEmail);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = SuperAdminEmail,
            Email = SuperAdminEmail,
            EmailConfirmed = true,
            FullName = SuperAdminFullName,
        };
        ThrowIfFailed(await userManager.CreateAsync(admin, password), "create the SuperAdmin user");
        ThrowIfFailed(await userManager.AddToRoleAsync(admin, Roles.SuperAdmin), "add the SuperAdmin role");
        logger.LogInformation("Seeded SuperAdmin user {Email}.", SuperAdminEmail);
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Seeding failed to {action}: {errors}");
        }
    }
}
```

Why a config switch instead of `IsDevelopment()`: Development sets `Migrate` (CLAUDE.md: migrations at startup in Development only), the test host sets `EnsureCreated` (SQLite cannot run the SQL Server migration), Production leaves it unset (migrations are applied by the deployment, seed still runs).

**Create file: `server/src/Crm.Infrastructure/DependencyInjection.cs`**

```csharp
using Crm.Application.Auth;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    /// <summary>EF Core (SQL Server), ASP.NET Identity stores, the auth service, the clock and the DB initializer.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Read lazily from the final configuration, so the test host can swap the provider (SQLite).
        services.AddDbContext<CrmDbContext>((provider, options) =>
            options.UseSqlServer(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Crm")
                ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Crm' is not configured.")));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<CrmDbContext>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<CrmDbInitializer>();
        return services;
    }
}
```

`AddIdentityCore` (not `AddIdentity`): no cookie scheme, no `SignInManager` — the API is JWT-only. Other password rules keep Identity defaults (digit, upper, lower, non-alphanumeric).

### 6 — Api: JWT options, token generator, authentication, endpoints

**Create file: `server/src/Crm.Api/Auth/JwtOptions.cs`**

```csharp
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Auth;

/// <summary>Bound from the <c>Jwt</c> configuration section. <c>SigningKey</c> is a secret (user-secrets / env var).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>HS256 needs a key of at least 256 bits.</summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience)
        && Encoding.UTF8.GetByteCount(SigningKey) >= 32
        && AccessTokenLifetimeMinutes > 0;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
```

**Create file: `server/src/Crm.Api/Auth/JwtAccessTokenGenerator.cs`**

```csharp
using Crm.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Auth;

/// <summary>Issues HS256 JWT access tokens. Times come from the injected <see cref="TimeProvider"/>.</summary>
public sealed class JwtAccessTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IAccessTokenGenerator
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Generate(AccessTokenSubject subject)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.AccessTokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = subject.UserId.ToString(),
                [JwtRegisteredClaimNames.Email] = subject.Email,
                [JwtRegisteredClaimNames.Name] = subject.FullName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                [AuthClaimTypes.Role] = subject.Roles.ToArray(),
            },
        };

        return new AccessToken(Handler.CreateToken(descriptor), expiresAt);
    }
}

/// <summary>Claim names used in our tokens (inbound claim mapping is off, so these are the names endpoints read).</summary>
public static class AuthClaimTypes
{
    public const string UserId = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Role = "role";
}
```

**Create file: `server/src/Crm.Api/Auth/AuthenticationExtensions.cs`**

```csharp
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Auth;

public static class AuthenticationExtensions
{
    /// <summary>JWT bearer authentication + authorization. Fails at startup when the <c>Jwt</c> section is invalid.</summary>
    public static IServiceCollection AddCrmAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => options.IsValid(),
                "Jwt settings are invalid: Issuer and Audience are required, SigningKey must be at least 32 bytes " +
                "(set it with dotnet user-secrets or the Jwt__SigningKey environment variable).")
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwtOptions, timeProvider) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = AuthClaimTypes.Email,
                    RoleClaimType = AuthClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // The default lifetime check reads the system clock; use the injected TimeProvider
                    // (CLAUDE.md: all time through TimeProvider) so tests can control token expiry.
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                        IsWithinLifetime(notBefore, expires, parameters.ClockSkew, timeProvider.GetUtcNow().UtcDateTime),
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>A token without <c>exp</c> is rejected; <c>nbf</c> is optional.</summary>
    private static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, TimeSpan clockSkew, DateTime utcNow) =>
        expires is not null
        && expires.Value.ToUniversalTime() > utcNow - clockSkew
        && (notBefore is null || notBefore.Value.ToUniversalTime() <= utcNow + clockSkew);
}
```

Key settings: `MapInboundClaims = false` keeps the short claim names (`sub`, `email`, `role`) so `RequireRole("SuperAdmin")` (CRM-7) works with `RoleClaimType = "role"`; `ValidAlgorithms` pins HS256 (rejects `alg: none` / algorithm confusion); `ClockSkew` 30 s instead of the 5-minute default. **No fallback policy**: an unknown route must stay **404** (CRM-5 test `KnownFailures_ReturnMatchingProblemDetails` with `/api/does-not-exist`); protection is explicit per endpoint and guarded by `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous`.

**Create file: `server/src/Crm.Api/Endpoints/AuthEndpoints.cs`**

```csharp
using System.Security.Claims;
using Crm.Api.Auth;
using Crm.Application.Auth;

namespace Crm.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, IAuthService authService, CancellationToken cancellationToken) =>
                Results.Ok(await authService.LoginAsync(request, cancellationToken)))
            .AllowAnonymous()
            .WithName("Login");

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new CurrentUserResponse(
                Guid.Parse(user.FindFirstValue(AuthClaimTypes.UserId)!),
                user.FindFirstValue(AuthClaimTypes.Email) ?? string.Empty,
                user.FindFirstValue(AuthClaimTypes.Name) ?? string.Empty,
                [.. user.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value)])))
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return app;
    }
}
```

**File: `server/src/Crm.Api/Endpoints/HealthEndpoints.cs`** — lines 7–8 become:

```csharp
        app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok")))
           .AllowAnonymous()
           .WithName("GetHealth");
```

**File: `server/src/Crm.Api/ErrorHandling/GlobalExceptionHandler.cs`** — add this arm **before line 63** (`NotFoundException notFound => …`). No new `using`: `Crm.Application.Common.Exceptions` is already imported at line 1.

```csharp
        UnauthorizedException unauthorized => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication failed.",
            Detail = unauthorized.Message,
        },
```

**File: `server/src/Crm.Api/Program.cs`** — final content:

```csharp
using Crm.Api.Auth;
using Crm.Api.Endpoints;
using Crm.Api.ErrorHandling;
using Crm.Application;
using Crm.Infrastructure;
using Crm.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCrmErrorHandling();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddCrmAuthentication(builder.Configuration);

var app = builder.Build();

app.UseCrmErrorHandling();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<CrmDbInitializer>().InitializeAsync(CancellationToken.None);
}

app.Run();

// Exposes Program to WebApplicationFactory<Program> in Crm.Api.IntegrationTests.
public partial class Program;
```

**File: `server/src/Crm.Api/appsettings.json`** — final content (non-secret JWT settings; the EF command logger is lowered because Identity logs every SQL statement at Information):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Jwt": {
    "Issuer": "Crm.Api",
    "Audience": "Crm.Client",
    "AccessTokenLifetimeMinutes": 60
  }
}
```

**File: `server/src/Crm.Api/appsettings.Development.json`** — final content (**double backslash** in JSON):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Crm": "Server=(localdb)\\MSSQLLocalDB;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Database": {
    "StartupAction": "Migrate"
  }
}
```

**Never** put `Jwt:SigningKey` or `Seed:SuperAdminPassword` in either appsettings file.

### 7 — First migration

From `server/`:

```bash
dotnet build
dotnet ef migrations add InitialIdentity --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations
```

Expected output ends with `Done. To undo this action, use 'ef migrations remove'` and creates three files in `server/src/Crm.Infrastructure/Persistence/Migrations/`: `<timestamp>_InitialIdentity.cs`, `<timestamp>_InitialIdentity.Designer.cs`, `CrmDbContextModelSnapshot.cs` (namespace `Crm.Infrastructure.Persistence.Migrations`; `AspNetUsers` has `FullName nvarchar(200) NOT NULL`). Commit them unchanged. **Do not** hand-edit migration files.

Run `dotnet test` → **Green**: **40 passed** (12 unit + 28 integration: 14 from CRM-5 incl. health, 5 login, 5 protected-endpoint, 1 expiry, 2 seed, 1 JWT config).

Optional proof that AC 5 really depends on the clock wiring: temporarily delete the `LifetimeValidator = …` line, run `dotnet test --filter TokenExpiryTests` → it fails with `Expected: Unauthorized, Actual: OK`; restore the line.

---

## Frontend Tasks

All commands run from `client/`. **No new npm packages.** Follow `vercel-react-best-practices` (direct imports, no barrel `index.ts`).

### 1 — Tests first (Red)

**File: `client/src/test/setup.ts`** — final content:

```ts
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

afterEach(() => {
  cleanup()
  localStorage.clear()
})
```

**Create file: `client/src/auth/session.test.ts`**

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearSession, getAccessToken, saveSession, subscribeToSession } from './session'

const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

describe('session', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('has no token when nothing is stored', () => {
    expect(getAccessToken()).toBeNull()
  })

  it('returns the saved token until it expires', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-01T10:00:00Z'))
    saveSession('token-1', '2026-01-01T11:00:00Z')

    expect(getAccessToken()).toBe('token-1')

    vi.setSystemTime(new Date('2026-01-01T11:00:01Z'))
    expect(getAccessToken()).toBeNull()
  })

  it('notifies subscribers on save and clear, and forgets the token on clear', () => {
    const listener = vi.fn()
    const unsubscribe = subscribeToSession(listener)

    saveSession('token-2', inOneHour())
    clearSession()
    unsubscribe()
    saveSession('token-3', inOneHour())

    expect(listener).toHaveBeenCalledTimes(2)
    expect(getAccessToken()).toBe('token-3')
    clearSession()
    expect(getAccessToken()).toBeNull()
  })

  it('ignores corrupt stored data', () => {
    localStorage.setItem('crm.session', '{not json')

    expect(getAccessToken()).toBeNull()
  })
})
```

**Create file: `client/src/api/client.auth.test.ts`** (separate from `client.test.ts`, which stays unchanged)

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { getAccessToken, saveSession } from '../auth/session'
import { apiGet, apiPost } from './client'

const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

function okJson(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
}

function sentHeaders(fetchMock: ReturnType<typeof vi.fn>): Record<string, string> {
  return (fetchMock.mock.calls[0][1] as RequestInit).headers as Record<string, string>
}

describe('API client authentication', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the bearer token when signed in', async () => {
    saveSession('abc.def.ghi', inOneHour())
    const fetchMock = vi.fn().mockResolvedValue(okJson({}))
    vi.stubGlobal('fetch', fetchMock)

    await apiGet('/api/auth/me')

    expect(sentHeaders(fetchMock).Authorization).toBe('Bearer abc.def.ghi')
  })

  it('sends no Authorization header when signed out', async () => {
    const fetchMock = vi.fn().mockResolvedValue(okJson({}))
    vi.stubGlobal('fetch', fetchMock)

    await apiGet('/api/health')

    expect(sentHeaders(fetchMock)).not.toHaveProperty('Authorization')
  })

  it('signs out when the API rejects the token with 401', async () => {
    saveSession('expired-on-server', inOneHour())
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))

    await expect(apiGet('/api/auth/me')).rejects.toThrow('401')

    expect(getAccessToken()).toBeNull()
  })

  it('posts a JSON body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(okJson({ ok: true }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(apiPost('/api/auth/login', { email: 'a@b.c' })).resolves.toEqual({ ok: true })

    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect(init.method).toBe('POST')
    expect(init.body).toBe('{"email":"a@b.c"}')
    expect(sentHeaders(fetchMock)['Content-Type']).toBe('application/json')
  })
})
```

**File: `client/src/components/ApiErrorToaster.test.tsx`** — append this test inside the `describe` block, after the abort test (ends at line 58). `problemResponse` is the helper already defined at the top of the file.

```tsx
  it('does not show a toast for 401 (handled by the sign-in flow)', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(problemResponse(401, { status: 401, correlationId: 'corr-401' }))
        .mockResolvedValueOnce(problemResponse(500, { status: 500, correlationId: 'corr-500' })),
    )
    render(<ApiErrorToaster />)

    await expect(apiGet('/api/auth/me')).rejects.toThrow('401')
    await expect(apiGet('/api/anything')).rejects.toThrow('500')

    // The 500 toast proves the toaster is listening; the 401 one must not exist.
    expect(await screen.findByText('Reference: corr-500')).toBeInTheDocument()
    expect(screen.queryByText('Reference: corr-401')).not.toBeInTheDocument()
  })
```

**Create file: `client/src/App.auth.test.tsx`** — whole sign-in flow through the real API client against a fake `fetch`.

```tsx
import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'

const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

function json(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

const me = { id: '1', email: 'admin@crm.local', fullName: 'System Administrator', roles: ['SuperAdmin'] }

/** Fake API: health is always ok; login accepts one password; /me needs the token login returned. */
function fakeApi() {
  return vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/health') return json(200, { status: 'ok' })
    if (path === '/api/auth/login') {
      const { password } = JSON.parse(String(init?.body)) as { password: string }
      return password === 'Admin#12345'
        ? json(200, { accessToken: 'good-token', tokenType: 'Bearer', expiresAt: inOneHour() })
        : json(401, { status: 401, title: 'Authentication failed.', correlationId: 'c-401' }, 'application/problem+json')
    }
    if (path === '/api/auth/me') {
      const auth = ((init?.headers ?? {}) as Record<string, string>).Authorization
      return auth === 'Bearer good-token' ? json(200, me) : json(401, { status: 401 }, 'application/problem+json')
    }
    return json(404, { status: 404 }, 'application/problem+json')
  })
}

function submitSignIn(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('App authentication', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the sign-in form when signed out', () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    expect(screen.getByRole('form', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('signs in with valid credentials and shows the current user', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    submitSignIn('admin@crm.local', 'Admin#12345')

    expect(await screen.findByText('Signed in as System Administrator')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Sign in' })).not.toBeInTheDocument()
  })

  it('shows an inline error and no toast for a wrong password', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    submitSignIn('admin@crm.local', 'wrong')

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(screen.queryByText('Something went wrong. Please try again.')).not.toBeInTheDocument()
  })

  it('returns to the sign-in form when the stored token is rejected', async () => {
    saveSession('expired-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('signs out', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)
    submitSignIn('admin@crm.local', 'Admin#12345')
    await screen.findByText('Signed in as System Administrator')

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
  })
})
```

(`fireEvent` comes with `@testing-library/react`; `@testing-library/user-event` is **not** installed and must not be added for this story.)

Run `npm test` → **Red** (`./session`, `./auth/session`, `apiPost` do not exist; the 401 toaster test fails because a toast with "Reference: corr-401" appears).

### 2 — Session store (Green, part 1)

**Create file: `client/src/auth/session.ts`**

```ts
/**
 * The only place that stores the access token (CLAUDE.md: "Auth token kept by client/src/auth/").
 * Components never read the token: they use useIsAuthenticated(); the API client calls getAccessToken().
 */
const STORAGE_KEY = 'crm.session'

interface StoredSession {
  accessToken: string
  /** ISO 8601 UTC, from the login response `expiresAt`. */
  expiresAt: string
}

type Listener = () => void

const listeners = new Set<Listener>()

function readSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<StoredSession>
    return typeof parsed.accessToken === 'string' && typeof parsed.expiresAt === 'string'
      ? { accessToken: parsed.accessToken, expiresAt: parsed.expiresAt }
      : null
  } catch {
    return null
  }
}

function notify() {
  listeners.forEach((listener) => listener())
}

/** The current token, or null when signed out or when the stored token has expired. */
export function getAccessToken(): string | null {
  const session = readSession()
  if (!session) return null
  const expiresAt = Date.parse(session.expiresAt)
  if (Number.isNaN(expiresAt) || expiresAt <= Date.now()) return null
  return session.accessToken
}

export function saveSession(accessToken: string, expiresAt: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ accessToken, expiresAt } satisfies StoredSession))
  } catch {
    // Storage blocked (private mode): the user stays signed out.
  }
  notify()
}

export function clearSession(): void {
  try {
    localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Nothing stored.
  }
  notify()
}

/** Subscribe to sign-in / sign-out, including changes made in other browser tabs. */
export function subscribeToSession(listener: Listener): () => void {
  const onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key === STORAGE_KEY) listener()
  }
  listeners.add(listener)
  window.addEventListener('storage', onStorage)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', onStorage)
  }
}
```

**Create file: `client/src/auth/useIsAuthenticated.ts`**

```ts
import { useSyncExternalStore } from 'react'
import { getAccessToken, subscribeToSession } from './session'

function getIsAuthenticated(): boolean {
  return getAccessToken() !== null
}

/** True while a non-expired access token is stored. Re-renders on sign-in / sign-out. */
export function useIsAuthenticated(): boolean {
  return useSyncExternalStore(subscribeToSession, getIsAuthenticated)
}
```

### 3 — API client: bearer token, 401, POST (Green, part 2)

**File: `client/src/api/client.ts`** — keep lines 1–31 (listener + `fail` + `readProblem`) but change line 1's imports, and replace lines 33–60 (`request` + `apiGet`). Final content:

```ts
import { clearSession, getAccessToken } from '../auth/session'
import { ApiError, type ProblemDetails } from './errors'

type ApiErrorListener = (error: ApiError) => void

const errorListeners = new Set<ApiErrorListener>()

/**
 * Subscribe to every failed API call (used by ApiErrorToaster to show a toast).
 * Returns an unsubscribe function.
 */
export function onApiError(listener: ApiErrorListener): () => void {
  errorListeners.add(listener)
  return () => {
    errorListeners.delete(listener)
  }
}

function fail(error: ApiError): never {
  errorListeners.forEach((listener) => listener(error))
  throw error
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  const contentType = response.headers.get('Content-Type') ?? ''
  if (!contentType.includes('json')) return undefined
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return undefined
  }
}

interface RequestOptions {
  body?: unknown
  signal?: AbortSignal
}

async function request<T>(method: string, path: string, { body, signal }: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const accessToken = getAccessToken()
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`

  let response: Response
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    // Cancelled on purpose (component unmounted): not a failure the user must see.
    if (signal?.aborted) throw error
    return fail(new ApiError(`${method} ${path} failed: network error`, 0))
  }

  if (!response.ok) {
    // Token expired or revoked: sign out, so the app shows the sign-in form again.
    if (response.status === 401 && accessToken) clearSession()
    const problem = await readProblem(response)
    return fail(
      new ApiError(
        `${method} ${path} failed with status ${response.status}`,
        response.status,
        problem,
        response.headers.get('X-Correlation-Id') ?? undefined,
      ),
    )
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('GET', path, { signal })
}

export function apiPost<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  return request<T>('POST', path, { body, signal })
}
```

`client.ts` is still the **only** `fetch` caller. Later stories add `apiPut` / `apiDelete` the same way.

**Create file: `client/src/api/auth.ts`**

```ts
import { apiGet, apiPost } from './client'

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  tokenType: 'Bearer'
  /** ISO 8601 UTC. */
  expiresAt: string
}

export interface CurrentUser {
  id: string
  email: string
  fullName: string
  roles: string[]
}

export function login(request: LoginRequest): Promise<LoginResponse> {
  return apiPost<LoginResponse>('/api/auth/login', request)
}

export function getCurrentUser(signal?: AbortSignal): Promise<CurrentUser> {
  return apiGet<CurrentUser>('/api/auth/me', signal)
}
```

**Create file: `client/src/auth/sign-in.ts`** (separate from `session.ts` to avoid the import cycle `session → api/auth → client → session`)

```ts
import { login } from '../api/auth'
import { clearSession, saveSession } from './session'

/** Calls POST /api/auth/login and stores the token. Rejects with ApiError (401 = wrong email or password). */
export async function signIn(email: string, password: string): Promise<void> {
  const response = await login({ email, password })
  saveSession(response.accessToken, response.expiresAt)
}

export function signOut(): void {
  clearSession()
}
```

### 4 — No toast for 401 (Green, part 3)

**File: `client/src/components/ApiErrorToaster.tsx`** — inside the listener (line 14), before `toast.error(…)`:

```tsx
      onApiError((error) => {
        // 401 is not a toast: the login form shows wrong credentials inline, and an expired
        // session clears the token so the app shows the sign-in form again (CRM-2).
        if (error.status === 401) return
        toast.error(getApiErrorMessage(error), { description: getApiErrorDescription(error) })
      }),
```

`client/src/api/error-messages.ts` stays unchanged (no 401 text needed).

### 5 — Minimal sign-in UI (Green, part 4)

**Create file: `client/src/features/auth/auth-messages.ts`**

```ts
// Temporary English text. CRM-4 moves these strings to client/src/i18n/{en,ar}.json.
export const authMessages = {
  signInTitle: 'Sign in',
  email: 'Email',
  password: 'Password',
  signIn: 'Sign in',
  signingIn: 'Signing in…',
  invalidCredentials: 'Invalid email or password.',
  signedInAs: (name: string) => `Signed in as ${name}`,
  signOut: 'Sign out',
  account: 'Account',
}
```

**Create file: `client/src/features/auth/LoginForm.tsx`**

```tsx
import { useState, type FormEvent } from 'react'
import { isApiError } from '../../api/errors'
import { signIn } from '../../auth/sign-in'
import { authMessages } from './auth-messages'

/** Minimal sign-in form. CRM-3 replaces it with the styled login page (shadcn/ui, react-hook-form + zod). */
export function LoginForm() {
  const [error, setError] = useState<string | null>(null)
  const [isPending, setIsPending] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setError(null)
    setIsPending(true)
    try {
      await signIn(String(form.get('email') ?? ''), String(form.get('password') ?? ''))
      // Success: the session store changes and App renders the signed-in view instead of this form.
    } catch (caught) {
      // Wrong credentials are shown here; every other failure already raised a toast (ApiErrorToaster).
      if (isApiError(caught) && caught.status === 401) setError(authMessages.invalidCredentials)
      setIsPending(false)
    }
  }

  return (
    <form aria-labelledby="sign-in-title" onSubmit={handleSubmit}>
      <h2 id="sign-in-title">{authMessages.signInTitle}</h2>
      <label>
        {authMessages.email}
        <input name="email" type="email" autoComplete="username" required />
      </label>
      <label>
        {authMessages.password}
        <input name="password" type="password" autoComplete="current-password" required />
      </label>
      {error ? <p role="alert">{error}</p> : null}
      <button type="submit" disabled={isPending}>
        {isPending ? authMessages.signingIn : authMessages.signIn}
      </button>
    </form>
  )
}
```

Use `onSubmit` (not a React 19 form `action`): form actions reset the uncontrolled inputs after every submit, which would wipe the email on a wrong password.

**Create file: `client/src/features/auth/CurrentUserPanel.tsx`**

```tsx
import { useEffect, useState } from 'react'
import { getCurrentUser, type CurrentUser } from '../../api/auth'
import { signOut } from '../../auth/sign-in'
import { authMessages } from './auth-messages'

/** Shows who is signed in (GET /api/auth/me, a protected endpoint) and a sign-out button. */
export function CurrentUserPanel() {
  const [user, setUser] = useState<CurrentUser | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    getCurrentUser(controller.signal)
      .then(setUser)
      // 401 already cleared the session (App shows the sign-in form); other failures raised a toast.
      .catch(() => {})
    return () => controller.abort()
  }, [])

  return (
    <section aria-label={authMessages.account}>
      {user ? <p>{authMessages.signedInAs(user.fullName)}</p> : null}
      <button type="button" onClick={signOut}>
        {authMessages.signOut}
      </button>
    </section>
  )
}
```

**File: `client/src/App.tsx`** — final content:

```tsx
import { useEffect, useState } from 'react'
import { getHealth } from './api/health'
import { useIsAuthenticated } from './auth/useIsAuthenticated'
import { ApiErrorToaster } from './components/ApiErrorToaster'
import { CurrentUserPanel } from './features/auth/CurrentUserPanel'
import { LoginForm } from './features/auth/LoginForm'

type ApiState = 'loading' | 'unavailable' | string

function App() {
  const [apiStatus, setApiStatus] = useState<ApiState>('loading')
  const isAuthenticated = useIsAuthenticated()

  useEffect(() => {
    const controller = new AbortController()
    getHealth(controller.signal)
      .then((health) => setApiStatus(health.status))
      .catch(() => {
        if (!controller.signal.aborted) setApiStatus('unavailable')
      })
    return () => controller.abort()
  }, [])

  // Temporary placeholder text: i18n arrives in CRM-4, layout + routing in CRM-3.
  return (
    <>
      <main>
        <h1>Customer Support CRM</h1>
        <p>
          API status: <strong>{apiStatus}</strong>
        </p>
        {isAuthenticated ? <CurrentUserPanel /> : <LoginForm />}
      </main>
      <ApiErrorToaster />
    </>
  )
}

export default App
```

No styling classes are added (CRM-3 owns styling); therefore no colors and no physical-direction classes are introduced.

Run `npm test` → **Green**: **25 passed** in 8 files (2 `health.test.ts`, 2 `App.test.tsx`, 3 `client.test.ts`, 4 `client.auth.test.ts`, 4 `session.test.ts`, 4 `ApiErrorToaster.test.tsx`, 1 `App.toast.test.tsx`, 5 `App.auth.test.tsx`).

### 6 — How later stories build on this (write nothing here; for later planners)

- **CRM-3** replaces `LoginForm` / `CurrentUserPanel` and the inline auth switch in `App.tsx` with the styled login page, layout and `react-router` routes (redirect to `/login` when `useIsAuthenticated()` is false). It reuses `signIn`, `signOut`, `useIsAuthenticated` and `getCurrentUser` as they are, and must keep the rule "no toast on 401".
- **CRM-4** moves `client/src/features/auth/auth-messages.ts` into `client/src/i18n/{en,ar}.json`.
- **CRM-6 (users)** manages `ApplicationUser` through `UserManager<ApplicationUser>` in Infrastructure (Application talks to it through an interface, like `IAuthService`). New user columns (e.g. `IsActive`) come with a **new migration** (`dotnet ef migrations add <Name> …` — same command as above); `AuthService.LoginAsync` must then also reject inactive users with the same `UnauthorizedException` message.
- **CRM-7 (roles & permissions)** builds policies on `Roles` constants / the `role` claim (`RequireAuthorization(policy)`, `RoleClaimType = "role"` already set). Permission claims, if added, go into `JwtAccessTokenGenerator`.
- **Every later endpoint group** calls `.RequireAuthorization()` (or `.AllowAnonymous()` with a reason). `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous` fails otherwise.
- **Every later integration test** of a protected endpoint uses `factory.CreateAuthenticatedClient(await factory.LoginAsync())`; tests that need another role create a user through `UserManager<ApplicationUser>` in a scope (see `Login_AfterFiveWrongPasswords_…`).
- Later entities are added to `CrmDbContext` (with `IsDeleted` query filters per CLAUDE.md) and each change gets its own migration.

---

## Edge Cases & Failure Modes

- **Unknown email vs wrong password vs locked-out user** → identical 401 + "Invalid email or password." (`AuthService.LoginAsync`), so attackers cannot probe which emails exist. Covered by `Login_WithWrongPassword_…`, `Login_WithUnknownEmail_…`, `Login_AfterFiveWrongPasswords_…`. Remaining timing difference (no password hash for unknown emails) is accepted for an internal staff app.
- **Brute force** → Identity lockout after 5 failures for 15 minutes (`DependencyInjection.AddInfrastructure`). Lockout state is per user in the DB. No IP rate limiting in this story.
- **Missing / invalid JSON fields** → `LoginRequestValidator` → 400 with `email` / `password` keys (CRM-5 contract). Covered by `Login_WithMissingFields_Returns400WithFieldErrors`.
- **No / malformed / forged / expired token** → JwtBearer challenge → empty 401 → `UseStatusCodePages()` → ProblemDetails with `correlationId` and `WWW-Authenticate: Bearer`. Covered by the four `ProtectedEndpoint_*` 401 tests and `TokenExpiryTests`.
- **Token lifetime vs clock** → the default IdentityModel check uses the system clock; `LifetimeValidator` in `AuthenticationExtensions.cs` uses the injected `TimeProvider` (30 s skew). Removing it makes `ProtectedEndpoint_WithExpiredToken_Returns401` fail (seen while planning).
- **`alg: none` / algorithm confusion** → `ValidAlgorithms = [HmacSha256]` rejects any other algorithm.
- **Missing or short `Jwt:SigningKey`** → `ValidateOnStart` throws `OptionsValidationException` when the host starts (clear message pointing to user-secrets). Covered by `Startup_WithTooShortSigningKey_Fails`. It does **not** run during `dotnet ef` (host stops at `Build()`).
- **Missing `Seed:SuperAdminPassword`** → warning log, no admin user, app still starts (CLAUDE.md: missing credentials must not crash startup). Roles are still seeded.
- **Seed password violates the Identity password policy** → `CrmDbInitializer.ThrowIfFailed` throws `InvalidOperationException` listing the Identity errors at startup. Use at least 8 chars with upper, lower, digit and symbol.
- **Missing `ConnectionStrings:Crm`** outside tests → `InvalidOperationException("Connection string 'ConnectionStrings:Crm' is not configured.")` when the first `CrmDbContext` is created (at startup, by the initializer). Read lazily so the test host can replace the provider.
- **Production database not migrated** → `Database:StartupAction` is unset in Production, so seeding fails fast on missing tables. Production deployment must apply migrations first (out of scope).
- **Development-environment integration tests** (`ErrorHandlingTests` with `UseEnvironment("Development")`) → they load `appsettings.Development.json` (`Migrate`, SQL Server) **and the developer's user-secrets**; the factory's in-memory config (added later) overrides `Database:StartupAction` and `Jwt:SigningKey`, and the SQL Server registration is removed — verified green.
- **Two DB providers registered** → happens if only `DbContextOptions<CrmDbContext>` is removed in the factory; remove `IDbContextOptionsConfiguration<CrmDbContext>` too (`CrmApiFactory.ConfigureWebHost`).
- **Shared SQLite connection** → one connection per factory instance; `WithWebHostBuilder` hosts reuse it (`EnsureCreated` no-ops, seeding is idempotent — `Initializer_RunTwice_DoesNotDuplicateData`). Tests inside one class run sequentially, so the connection is never used concurrently.
- **Clock moved by one test leaking into another** → `TokenExpiryTests` has its own class fixture (own `CrmApiFactory`, own `FakeTimeProvider`).
- **New endpoint without authorization metadata** → `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous` fails. Unknown routes still return 404 because there is no fallback policy.
- **Client: token expires while the tab is open** → `getAccessToken()` returns `null` after `expiresAt`; the next render or request sees "signed out". A server-side 401 (e.g. clock difference) clears the session in `client.ts`.
- **Client: localStorage blocked or corrupt** → `readSession` / `saveSession` swallow errors; user simply appears signed out. Covered by `ignores corrupt stored data`.
- **Client: XSS could read localStorage** → accepted trade-off for this phase (no HttpOnly-cookie flow); React escaping + no `dangerouslySetInnerHTML` keep the risk low. Revisit with refresh tokens later.
- **Client: sign-out in another tab** → the `storage` event in `subscribeToSession` re-renders this tab to the sign-in form.
- **Client: StrictMode double effect** → `CurrentUserPanel` aborts the first `/me` call; aborted requests never toast (CRM-5 behaviour).
- **Client: login 400 (e.g. bad email format bypassing `type="email"`)** → generic CRM-5 toast "The request is invalid. Check the entered data." (no inline field errors until CRM-3's react-hook-form + zod).

---

## Test Plan

1. **Unit** — `server/tests/Crm.UnitTests/Auth/LoginRequestValidatorTests.cs`: `ValidRequest_HasNoErrors`, `InvalidRequest_ReportsTheField` ×5.
2. **Unit (modified)** — `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs`: `ForbiddenPrefixes` gains `Microsoft.Extensions.Identity`; both tests stay green.
3. **Integration** — `server/tests/Crm.Api.IntegrationTests/Auth/LoginTests.cs`: `Login_WithValidCredentials_Returns200WithAccessToken` (AC 1), `Login_WithWrongPassword_Returns401WithoutToken` (AC 2), `Login_WithUnknownEmail_Returns401WithSameMessage`, `Login_WithMissingFields_Returns400WithFieldErrors`, `Login_AfterFiveWrongPasswords_IsLockedOutEvenWithCorrectPassword`.
4. **Integration** — `server/tests/Crm.Api.IntegrationTests/Auth/ProtectedEndpointTests.cs`: `ProtectedEndpoint_WithoutToken_Returns401ProblemDetails` (AC 3), `ProtectedEndpoint_WithValidToken_Returns200WithCurrentUser` (AC 4), `ProtectedEndpoint_WithTokenSignedByAnotherKey_Returns401`, `ProtectedEndpoint_WithMalformedAuthorizationHeader_Returns401`, `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous`.
5. **Integration** — `server/tests/Crm.Api.IntegrationTests/Auth/TokenExpiryTests.cs`: `ProtectedEndpoint_WithExpiredToken_Returns401` (AC 5).
6. **Integration** — `server/tests/Crm.Api.IntegrationTests/Auth/SeedTests.cs`: `Startup_SeedsRolesAndSuperAdmin`, `Initializer_RunTwice_DoesNotDuplicateData`.
7. **Integration** — `server/tests/Crm.Api.IntegrationTests/Auth/JwtConfigurationTests.cs`: `Startup_WithTooShortSigningKey_Fails`.
8. **Integration (unchanged, must stay green)** — `HealthEndpointTests.cs`, `ErrorHandling/ErrorHandlingTests.cs`, `ErrorHandling/CorrelationIdTests.cs` (they now run on the SQLite-backed `CrmApiFactory`).
9. **Unit (frontend)** — `client/src/auth/session.test.ts`: no token, expiry, subscribe/clear, corrupt storage.
10. **Unit (frontend)** — `client/src/api/client.auth.test.ts`: bearer header when signed in (AC 4 client side), none when signed out, 401 clears the session (AC 5 client side), `apiPost` JSON body.
11. **Component (modified)** — `client/src/components/ApiErrorToaster.test.tsx`: `does not show a toast for 401 (handled by the sign-in flow)`.
12. **Component (app level)** — `client/src/App.auth.test.tsx`: form when signed out, successful sign-in shows "Signed in as System Administrator" (AC 1 + 4 end-to-end in the UI), wrong password shows inline alert and no toast (AC 2), rejected stored token returns to the form (AC 5), sign out.
13. **Unchanged, must stay green** — `client/src/api/health.test.ts`, `client/src/api/client.test.ts`, `client/src/App.test.tsx`, `client/src/App.toast.test.tsx`.
14. **Manual smoke** — Verification step 7.

---

## Migration / Rollback

- **Schema:** migration `InitialIdentity` creates the ASP.NET Identity tables (`AspNetUsers` with `FullName`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`) in `CustomerSupportCrm` on LocalDB. It is applied automatically on the first `dotnet run` in Development.
- **Rollback (local):** stop the API, then from `server/`: `dotnet ef database drop --project src/Crm.Infrastructure --startup-project src/Crm.Api --force`. To discard the migration before committing: `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api`.
- **Half-applied state:** EF applies each migration in a transaction; if startup crashes during seeding (e.g. weak seed password), the schema exists and roles may exist — fix the secret and restart; the initializer is idempotent.
- **Secrets:** stored outside the repo in the user-secrets store for `Crm.Api`'s `UserSecretsId`. Removing them makes startup fail (JWT) or skip the admin (seed) — by design.

---

## Verification Steps

1. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings.
2. **Backend tests:** in `server/` run `dotnet test` — **40 passed** (12 unit, 28 integration), 0 failed. LocalDB is not needed.
3. **Frontend tests:** in `client/` run `npm test` — **25 passed** in 8 files, process exits.
4. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed.
5. **Frontend lint:** in `client/` run `npm run lint` — oxlint reports 0 warnings and 0 errors.
6. **Secrets (once per machine):** in `server/` run
   - `dotnet user-secrets set "Jwt:SigningKey" "<random string of at least 32 characters>" --project src/Crm.Api`
   - `dotnet user-secrets set "Seed:SuperAdminPassword" "<password with upper, lower, digit, symbol, 8+ chars>" --project src/Crm.Api`
   - (PowerShell one-liner for a key: `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))`.)
7. **Manual smoke:**
   - Terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http` — log shows "Seeded SuperAdmin user admin@crm.local." on the first run; database `CustomerSupportCrm` now exists on `(localdb)\MSSQLLocalDB`.
   - `curl -i http://localhost:5080/api/auth/me` → `401`, `Content-Type: application/problem+json`, `WWW-Authenticate: Bearer`.
   - `curl -i -X POST http://localhost:5080/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"admin@crm.local\",\"password\":\"<seed password>\"}"` → `200` with `accessToken`, `"tokenType":"Bearer"`, `expiresAt` ≈ now + 60 min.
   - Same call with a wrong password → `401` ProblemDetails, `"detail":"Invalid email or password."`, no `accessToken`.
   - `curl -i http://localhost:5080/api/auth/me -H "Authorization: Bearer <accessToken>"` → `200` `{"id":…,"email":"admin@crm.local","fullName":"System Administrator","roles":["SuperAdmin"]}`.
   - Terminal 2: `cd client && npm run dev`, open `http://localhost:5173` → "API status: ok" and the sign-in form. Wrong password → inline "Invalid email or password.", **no toast**. Correct password → "Signed in as System Administrator" + "Sign out". Reload → still signed in. "Sign out" → form again.
8. **Regression:** `git status` shows no changes under `.claude/`, `.mcp.json`, `CLAUDE.md`, `client/src/index.css`; no secret values in any committed file (`git grep -n "SigningKey\|SuperAdminPassword" -- server/src` shows only option names, no values).

---

## Done Criteria

- [ ] Valid email + password → 200 with a JWT access token (`Login_WithValidCredentials_Returns200WithAccessToken` green).
- [ ] Wrong password → 401 ProblemDetails, no token (`Login_WithWrongPassword_Returns401WithoutToken` green).
- [ ] Protected endpoint without a token → 401 (`ProtectedEndpoint_WithoutToken_Returns401ProblemDetails` green).
- [ ] Protected endpoint with a valid token → 200 (`ProtectedEndpoint_WithValidToken_Returns200WithCurrentUser` green).
- [ ] Expired token → 401 (`ProtectedEndpoint_WithExpiredToken_Returns401` green, clock via injected `TimeProvider`).
- [ ] `CrmDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` in `Crm.Infrastructure/Persistence/`; migration `InitialIdentity` in `Crm.Infrastructure/Persistence/Migrations/`; Development migrates LocalDB at startup.
- [ ] Roles `SuperAdmin`, `Admin`, `Supervisor`, `Agent` and user `admin@crm.local` seeded idempotently (`SeedTests` green).
- [ ] `Jwt:SigningKey` / `Seed:SuperAdminPassword` only in user-secrets / env vars; `Crm.Api.csproj` has a `UserSecretsId`.
- [ ] Every `/api/*` endpoint declares `RequireAuthorization()` or `AllowAnonymous()` (guard test green).
- [ ] `CrmApiFactory` uses SQLite in-memory + `FakeTimeProvider` and offers `LoginAsync` / `CreateAuthenticatedClient`.
- [ ] Client: token only in `client/src/auth/session.ts`, attached by `client/src/api/client.ts`; 401 never toasts; 401 on a signed-in call signs out; minimal sign-in form + sign-out work.
- [ ] No new npm packages; NuGet additions exactly as in Backend Task 1.
- [ ] `dotnet test` (40) and `npm test` (25) pass; `dotnet build`, `npm run build` and `npm run lint` are clean.
- [ ] Committed on `feature/crm-2-authentication` with message `CRM-2: authentication with JWT`.
- [ ] Overview `00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 04.**
