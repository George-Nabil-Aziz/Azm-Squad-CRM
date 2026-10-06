# Story 02 — Global error handling & validation (Story: CRM-5)

## Prerequisites

- Story 01 completed: [01-story-project-skeleton-CRM-1.md](01-story-project-skeleton-CRM-1.md) (CRM-1) — `server/` solution, `client/` app, `GET /api/health`, typed API client in `client/src/api/client.ts`. Merged to `main`.
- Work on branch **`feature/crm-5-error-handling`** (already created from `main`).
- Phase 1 order: **CRM-5 (this)** → CRM-2 (JWT auth) → CRM-3 (layout + shadcn/ui) → CRM-4 (ar/en + RTL) → CRM-6 → CRM-7. This story therefore runs **before** auth, Tailwind, shadcn/ui and i18n exist.
- **Shared contract created here** (every later story depends on it): the ProblemDetails response shape (with `correlationId` extension), the `X-Correlation-Id` header, the four Application exceptions, `ValidateOrThrowAsync`, the `CrmApiFactory` test host, and the client `ApiError` + `onApiError` toast hook. Change them only together with the stories that consume them.

---

## Story Goal

Give the API and the client one consistent, safe way to report failures:

1. Invalid input → **400** `application/problem+json` with field-level `errors` (camelCase keys matching the JSON request), produced by FluentValidation in the Application layer and mapped by one global handler in the API.
2. Any unhandled exception → **500** `application/problem+json` with a generic title; **no exception message or stack trace outside Development**.
3. Every API failure in the client raises an **error toast** (sonner), including a "Reference: <correlationId>" line so users can quote it to support.
4. Every request has a **correlation id** (`X-Correlation-Id` request header or a generated one): echoed in the response header, included in every ProblemDetails body, and present in every log line of the request (logging scope + message template).

Also mapped now (architecture decision in `CLAUDE.md`): `NotFoundException` → 404, `ConflictException` → 409, `ForbiddenException` → 403, malformed JSON → 400, unknown route → 404 — all as ProblemDetails.

**Not in scope:** auth / 401 (CRM-2), Tailwind + shadcn init (CRM-3), i18n / RTL of messages (CRM-4), React Query and inline form field errors (later feature stories), external log sinks.

---

## Context — Read These Files First

1. `CLAUDE.md` — **Backend rules** ("Errors: RFC 7807 ProblemDetails. Validation errors → 400 with field errors", layers, async + `CancellationToken`), **Architecture decisions** → *Application layer* (FluentValidation; exceptions in `Crm.Application/Common/Exceptions` mapped by the CRM-5 handler) and *Integration tests* (one shared `CrmApiFactory` in `Crm.Api.IntegrationTests/Infrastructure/`, environment `Testing`), **Frontend rules** (shadcn `sonner` for toasts, API calls only through `client/src/api`, no hard-coded hex colors, logical Tailwind classes), **TDD**.
2. `.squad/stories/01-foundation/CRM-5/intake.md` — acceptance criteria 1–4 and **Out of scope**.
3. `server/src/Crm.Api/Program.cs` — whole file (19 lines). Line 5 `AddOpenApi()`, lines 9–12 Development-only `MapOpenApi()`, line 14 `MapHealthEndpoints()`, line 19 `public partial class Program;` (keep it). You will add service registration after line 5 and the error middleware before line 9.
4. `server/src/Crm.Application/Crm.Application.csproj` — lines 3–5 reference only `Crm.Domain`; no packages yet. FluentValidation goes here.
5. `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs` — lines 5–6 `ForbiddenPrefixes` (`Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, …). Application **must not** reference `Microsoft.AspNetCore.*`: the new exceptions and `ValidatorExtensions` use only BCL + FluentValidation; ProblemDetails mapping lives in `Crm.Api`.
6. `server/tests/Crm.Api.IntegrationTests/HealthEndpointTests.cs` — lines 7–8 use `WebApplicationFactory<Program>` directly; switch to the new `CrmApiFactory` (pattern for every later integration test).
7. `server/tests/Crm.Api.IntegrationTests/Crm.Api.IntegrationTests.csproj` — line 12 `Microsoft.AspNetCore.Mvc.Testing` **10.0.11** (brings the ASP.NET Core shared framework, so test-only endpoints compile in the test project).
8. `client/src/api/client.ts` — whole file (7 lines). `apiGet` throws a plain `Error("GET <path> failed with status <n>")`. You replace it with an `ApiError` and an error-listener hook; **keep the message format** — `client/src/api/health.test.ts` lines 19–23 assert `rejects.toThrow('503')`.
9. `client/src/App.tsx` — lines 20–27 render `<main>`; the toaster is mounted next to it. `client/src/App.test.tsx` line 6 mocks `./api/health` for the whole file — that is why the new app-level toast test lives in a **separate** file.
10. `client/tsconfig.app.json` — line 22 `"erasableSyntaxOnly": true`: **no TypeScript parameter properties / enums**. `ApiError` declares its fields explicitly (snippet below).
11. `client/.oxlintrc.json` — line 6 `react/only-export-components`: a `.tsx` file exports only components; helper functions live in `.ts` files.
12. Precedent: [01-story-project-skeleton-CRM-1.md](01-story-project-skeleton-CRM-1.md) — same test style (xUnit `[Fact]`, `Assert.*`, RTL `findByText`, `vi.stubGlobal('fetch', …)`).

Verified while planning (probe copy of the repo in the scratchpad, every snippet below compiled and all tests ran green): **FluentValidation 12.1.1** + **FluentValidation.DependencyInjectionExtensions 12.1.1** (latest on nuget.org); **sonner 2.0.8** (latest on npm; peer `react ^18 || ^19`); shadcn registry item `@shadcn/sonner` = a wrapper over `sonner` + `next-themes`. In .NET 10 the exception-handler middleware does **not** log an exception that an `IExceptionHandler` handled (probe: exactly one Error entry per failure) — so the handler logs it itself. In the `Testing`/`Production` environments a malformed JSON body produces an empty 400 that `UseStatusCodePages()` turns into ProblemDetails; in `Development` minimal APIs throw `BadHttpRequestException`, which the handler maps to 400. sonner's `<Toaster>` with the default `theme="light"` does not call `window.matchMedia`, so it works in jsdom without polyfills; its container is a `<section aria-label="Notifications alt+T">` and toasts are `<li>` elements.

---

## Backend Tasks

All commands run from `server/` unless stated.

### 1 — Packages

```bash
dotnet add src/Crm.Application package FluentValidation --version 12.1.1
dotnet add src/Crm.Application package FluentValidation.DependencyInjectionExtensions --version 12.1.1
```

Test projects get FluentValidation transitively through their project references — **do not** add it to them.

### 2 — Unit tests first (Red)

**Create file: `server/tests/Crm.UnitTests/Common/ValidatorExtensionsTests.cs`**

```csharp
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.UnitTests.Common;

public class ValidatorExtensionsTests
{
    private sealed record Address(string? City);

    private sealed record Person(string? FirstName, string? Email, Address Address);

    private sealed class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleFor(p => p.FirstName).NotEmpty().MaximumLength(3).Matches("^[A-Za-z]*$");
            RuleFor(p => p.Email).NotEmpty().EmailAddress();
            RuleFor(p => p.Address.City).NotEmpty();
        }
    }

    private readonly PersonValidator _validator = new();

    [Fact]
    public async Task ValidateOrThrowAsync_ValidInstance_DoesNotThrow()
    {
        var person = new Person("Ali", "ali@example.com", new Address("Riyadh"));

        await _validator.ValidateOrThrowAsync(person, CancellationToken.None);
    }

    [Fact]
    public async Task ValidateOrThrowAsync_InvalidInstance_ThrowsWithCamelCaseFieldErrors()
    {
        var person = new Person("", "not-an-email", new Address(""));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _validator.ValidateOrThrowAsync(person, CancellationToken.None));

        Assert.Equal(["address.city", "email", "firstName"], exception.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.All(exception.Errors.Values, messages => Assert.NotEmpty(messages));
    }

    [Fact]
    public async Task ValidateOrThrowAsync_SeveralFailuresOnOneField_GroupsThemUnderOneKey()
    {
        var person = new Person("1234", "ali@example.com", new Address("Riyadh"));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _validator.ValidateOrThrowAsync(person, CancellationToken.None));

        var messages = Assert.Single(exception.Errors).Value;
        Assert.Equal(2, messages.Length);
        Assert.True(exception.Errors.ContainsKey("firstName"));
    }

    [Fact]
    public void ApplicationExceptions_KeepTheirMessage()
    {
        Assert.Equal("x", new NotFoundException("x").Message);
        Assert.Equal("y", new ConflictException("y").Message);
        Assert.Equal("z", new ForbiddenException("z").Message);
    }
}
```

Run `dotnet test` → **Red** (compile errors: `Crm.Application.Common.*` does not exist).

### 3 — Application exceptions + validation helper (Green for unit tests)

**Create file: `server/src/Crm.Application/Common/Exceptions/ValidationException.cs`**

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>Request failed validation. Mapped to 400 ProblemDetails with field errors by the API.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// <summary>Field name (camelCase, as in the JSON request) → error messages.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
```

**Create file: `server/src/Crm.Application/Common/Exceptions/NotFoundException.cs`**

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>Requested resource does not exist. Mapped to 404 ProblemDetails.</summary>
public sealed class NotFoundException(string message) : Exception(message);
```

**Create files** `ConflictException.cs` (summary "Request conflicts with current state (duplicate, concurrency). Mapped to 409 ProblemDetails.") and `ForbiddenException.cs` (summary "Caller is authenticated but not allowed to do this. Mapped to 403 ProblemDetails.") in the same folder, **same shape** as `NotFoundException`.

**Create file: `server/src/Crm.Application/Common/Validation/ValidatorExtensions.cs`**

```csharp
using System.Text.Json;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Common.Validation;

public static class ValidatorExtensions
{
    /// <summary>
    /// Validates <paramref name="instance"/> and throws <see cref="ValidationException"/> with
    /// camelCase field names (matching the JSON request body) when it is invalid.
    /// </summary>
    public static async Task ValidateOrThrowAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(failure => ToCamelCasePath(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(),
                StringComparer.Ordinal);

        throw new ValidationException(errors);
    }

    // "Address.City" → "address.city", "Items[0].Name" → "items[0].name"
    private static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
```

The `using ValidationException = …` alias is **required**: `FluentValidation` also defines a `ValidationException`.

**Create file: `server/src/Crm.Application/DependencyInjection.cs`**

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application;

public static class DependencyInjection
{
    /// <summary>Registers Application-layer services: every FluentValidation validator in this assembly.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly, includeInternalTypes: true);
        return services;
    }
}
```

Usage contract for later stories: each feature service takes `IValidator<TRequest>` by constructor injection and calls `await validator.ValidateOrThrowAsync(request, cancellationToken)` as its first step. Validators live next to their request DTOs in `Crm.Application/<Feature>/`.

Run `dotnet test` → unit tests **Green** (6 passed incl. the 2 existing architecture tests).

### 4 — Integration test host + test-only endpoints

**Create file: `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs`**

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crm.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The one shared test host (CLAUDE.md "Integration tests"). Environment <c>Testing</c>.
/// Captures logs in <see cref="Logs"/> and maps test-only endpoints under <c>/_test</c>.
/// Later stories add SQLite in-memory, JWT settings, etc. here.
/// </summary>
public class CrmApiFactory : WebApplicationFactory<Program>
{
    public TestLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.AddTransient<IStartupFilter, TestEndpointsStartupFilter>();
            services.AddScoped<FluentValidation.IValidator<SampleRequest>, SampleRequestValidator>();
        });
    }
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Infrastructure/TestLoggerProvider.cs`**

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Crm.Api.IntegrationTests.Infrastructure;

public sealed record LogEntry(
    string Category, LogLevel Level, string Message, Exception? Exception, IReadOnlyList<object?> Scopes);

/// <summary>In-memory logger that records every entry together with its active scopes.</summary>
public sealed class TestLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class TestLogger(string category, TestLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            provider._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<object?>();
            provider._scopes.ForEachScope((scope, list) => list.Add(scope), scopes);
            provider._entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception, scopes));
        }
    }
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Infrastructure/TestEndpointsStartupFilter.cs`** — no feature endpoint with input exists yet, so the real `Program.cs` pipeline is exercised through endpoints that exist **only in the test host**. The filter runs `next(app)` (the whole `Program.cs` pipeline) first, so the error middleware wraps these endpoints exactly as it wraps real ones.

```csharp
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Crm.Api.IntegrationTests.Infrastructure;

public sealed record SampleRequest(string? Name, string? Email, int Age);

public sealed class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

/// <summary>Test-only endpoints under /_test/errors that trigger each kind of failure.</summary>
public sealed class TestEndpointsStartupFilter : IStartupFilter
{
    public const string SecretMessage = "secret-internal-detail-1234";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            var group = endpoints.MapGroup("/_test/errors");
            group.MapPost("/validate", async (SampleRequest request, IValidator<SampleRequest> validator,
                CancellationToken cancellationToken) =>
            {
                await validator.ValidateOrThrowAsync(request, cancellationToken);
                return Results.NoContent();
            });
            group.MapGet("/unhandled", IResult () => throw new InvalidOperationException(SecretMessage));
            group.MapGet("/not-found", IResult () => throw new NotFoundException("Customer 42 was not found."));
            group.MapGet("/conflict", IResult () => throw new ConflictException("Email already in use."));
            group.MapGet("/forbidden", IResult () => throw new ForbiddenException("Agents cannot delete customers."));
        });
    };
}
```

**File: `server/tests/Crm.Api.IntegrationTests/HealthEndpointTests.cs`** — lines 7–8: replace `WebApplicationFactory<Program>` with `CrmApiFactory` in both places, replace `using Microsoft.AspNetCore.Mvc.Testing;` (line 3) with `using Crm.Api.IntegrationTests.Infrastructure;`. Nothing else changes.

### 5 — Integration tests first (Red)

**Create file: `server/tests/Crm.Api.IntegrationTests/ErrorHandling/ErrorHandlingTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.ErrorHandling;

public class ErrorHandlingTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task InvalidRequest_Returns400ProblemDetailsWithFieldErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/_test/errors/validate",
            new { name = "", email = "not-an-email", age = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("name", problem.Errors.Keys);
        Assert.Contains("email", problem.Errors.Keys);
        Assert.All(problem.Errors.Values, messages => Assert.NotEmpty(messages));
    }

    [Fact]
    public async Task ValidRequest_PassesValidation()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/_test/errors/validate",
            new { name = "Sara", email = "sara@example.com", age = 30 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJson_Returns400ProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/_test/errors/validate",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MalformedJson_InDevelopment_Returns400ProblemDetails()
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var response = await client.PostAsync("/_test/errors/validate",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnhandledException_InProduction_Returns500ProblemDetailsWithoutStackTrace()
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

        var response = await client.GetAsync("/_test/errors/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TestEndpointsStartupFilter.SecretMessage, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(500, problem!.Status);
        Assert.Null(problem.Detail);
    }

    [Fact]
    public async Task UnhandledException_InDevelopment_IncludesExceptionDetail()
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var response = await client.GetAsync("/_test/errors/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains(TestEndpointsStartupFilter.SecretMessage, problem!.Detail);
    }

    [Theory]
    [InlineData("/_test/errors/not-found", HttpStatusCode.NotFound)]
    [InlineData("/_test/errors/conflict", HttpStatusCode.Conflict)]
    [InlineData("/_test/errors/forbidden", HttpStatusCode.Forbidden)]
    [InlineData("/api/does-not-exist", HttpStatusCode.NotFound)]
    public async Task KnownFailures_ReturnMatchingProblemDetails(string path, HttpStatusCode expected)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)expected, problem!.Status);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
    }
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/ErrorHandling/CorrelationIdTests.cs`**

```csharp
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Crm.Api.IntegrationTests.ErrorHandling;

public class CorrelationIdTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string Header = "X-Correlation-Id";

    [Fact]
    public async Task UnhandledException_IsLoggedWithCorrelationId()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/_test/errors/unhandled");
        request.Headers.Add(Header, "test-corr-500");

        var response = await client.SendAsync(request);

        Assert.Equal("test-corr-500", response.Headers.GetValues(Header).Single());
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("test-corr-500", problem!.Extensions["correlationId"]?.ToString());
        var entry = Assert.Single(factory.Logs.Entries,
            e => e.Level == LogLevel.Error && e.Message.Contains("test-corr-500"));
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains(entry.Scopes, scope => scope is IEnumerable<KeyValuePair<string, object>> pairs
            && pairs.Any(p => p.Key == "CorrelationId" && (string)p.Value == "test-corr-500"));
    }

    [Fact]
    public async Task MissingCorrelationHeader_GeneratesOne()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues(Header).Single());
    }

    [Fact]
    public async Task InvalidCorrelationHeader_IsReplacedByAGeneratedOne()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.TryAddWithoutValidation(Header, "bad id with spaces");

        var response = await client.SendAsync(request);

        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues(Header).Single());
    }
}
```

Run `dotnet test` → **Red**: the project compiles, but the new tests fail at runtime (no handler: validation → 500 without problem+json, no `X-Correlation-Id` header, no Error log with the id). `ValidRequest_PassesValidation` and `HealthEndpointTests` already pass — that is expected.

### 6 — Correlation id middleware (Green, part 1)

**Create file: `server/src/Crm.Api/ErrorHandling/CorrelationIdMiddleware.cs`**

```csharp
using System.Text.RegularExpressions;

namespace Crm.Api.ErrorHandling;

/// <summary>
/// Reads <c>X-Correlation-Id</c> from the request (or generates one), echoes it in the response header,
/// stores it in <see cref="HttpContext.Items"/> and opens a logging scope so every log line of the request carries it.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadValidHeader(context) ?? Guid.NewGuid().ToString("N");
        context.Items[ItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await next(context);
        }
    }

    /// <summary>Correlation id of the current request; falls back to <see cref="HttpContext.TraceIdentifier"/>.</summary>
    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id ? id : context.TraceIdentifier;

    private static string? ReadValidHeader(HttpContext context)
    {
        var value = context.Request.Headers[HeaderName].ToString();
        return AllowedFormat().IsMatch(value) ? value : null;
    }

    // Untrusted input: only short, log-safe ids are accepted; anything else is replaced by a new id.
    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex AllowedFormat();
}
```

### 7 — Global exception handler + registration (Green, part 2)

**Create file: `server/src/Crm.Api/ErrorHandling/GlobalExceptionHandler.cs`**

```csharp
using Crm.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.ErrorHandling;

/// <summary>
/// Turns every exception that escapes an endpoint into an RFC 7807 ProblemDetails response and logs it
/// with the request's correlation id. Never exposes exception details outside Development.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);
        var problem = ToProblemDetails(exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception,
                "Unhandled exception for {Method} {Path}. CorrelationId: {CorrelationId}",
                httpContext.Request.Method, httpContext.Request.Path, correlationId);

            if (environment.IsDevelopment())
            {
                problem.Detail = exception.ToString();
            }
        }
        else
        {
            logger.LogWarning(
                "Request {Method} {Path} failed with {StatusCode} ({ExceptionType}). CorrelationId: {CorrelationId}",
                httpContext.Request.Method, httpContext.Request.Path, problem.Status, exception.GetType().Name, correlationId);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails ToProblemDetails(Exception exception) => exception switch
    {
        ValidationException validation => new HttpValidationProblemDetails(
            validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
        },
        BadHttpRequestException badRequest => new ProblemDetails
        {
            Status = badRequest.StatusCode,
            Title = "The request is malformed.",
        },
        NotFoundException notFound => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "The requested resource was not found.",
            Detail = notFound.Message,
        },
        ConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The request conflicts with the current state.",
            Detail = conflict.Message,
        },
        ForbiddenException forbidden => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "You do not have permission to perform this action.",
            Detail = forbidden.Message,
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
        },
    };
}
```

Rule for later stories: the `Detail` of 404/409/403 is the exception message — **write those messages for end users** (no ids of other tenants, no SQL, no internals).

**Create file: `server/src/Crm.Api/ErrorHandling/ErrorHandlingExtensions.cs`**

```csharp
namespace Crm.Api.ErrorHandling;

public static class ErrorHandlingExtensions
{
    /// <summary>ProblemDetails for every error response, with <c>correlationId</c> and <c>instance</c> filled in.</summary>
    public static IServiceCollection AddCrmErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["correlationId"] =
                    CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>Order matters: correlation id first (outermost), then the exception handler, then status-code pages.</summary>
    public static WebApplication UseCrmErrorHandling(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
```

**File: `server/src/Crm.Api/Program.cs`** — final content:

```csharp
using Crm.Api.Endpoints;
using Crm.Api.ErrorHandling;
using Crm.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCrmErrorHandling();
builder.Services.AddApplication();

var app = builder.Build();

app.UseCrmErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();

app.Run();

// Exposes Program to WebApplicationFactory<Program> in Crm.Api.IntegrationTests.
public partial class Program;
```

**`app.UseCrmErrorHandling()` must stay the first middleware** — later stories add `UseAuthentication()` / `UseAuthorization()` **after** it so auth failures are also wrapped.

Run `dotnet test` → **Green**: 6 unit + 14 integration = **20 passed**.

---

## Frontend Tasks

All commands run from `client/`.

### 1 — Package

```bash
npm install sonner@^2.0.8
```

`sonner` lands in `dependencies`. This is the library the shadcn `sonner` component wraps — not an extra UI library. **Do not** install Tailwind, shadcn or `next-themes` in this story (CRM-3).

### 2 — Tests first (Red)

**Create file: `client/src/api/client.test.ts`**

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiGet, onApiError } from './client'
import { ApiError } from './errors'

describe('apiGet errors', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('throws ApiError with the ProblemDetails body and correlation id', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            status: 400,
            title: 'One or more validation errors occurred.',
            errors: { name: ["'Name' must not be empty."] },
            correlationId: 'corr-1',
          }),
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )

    const error = await apiGet('/api/x').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    const apiError = error as ApiError
    expect(apiError.status).toBe(400)
    expect(apiError.problem?.errors).toEqual({ name: ["'Name' must not be empty."] })
    expect(apiError.correlationId).toBe('corr-1')
  })

  it('throws ApiError without a body when the response is not JSON', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('Bad gateway', { status: 502 })))

    const error = (await apiGet('/api/x').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(502)
    expect(error.problem).toBeUndefined()
  })

  it('notifies subscribers once per failure and stops after unsubscribe', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 500 })))
    const listener = vi.fn()
    const unsubscribe = onApiError(listener)

    await apiGet('/api/x').catch(() => {})
    unsubscribe()
    await apiGet('/api/x').catch(() => {})

    expect(listener).toHaveBeenCalledTimes(1)
    expect(listener.mock.calls[0][0]).toBeInstanceOf(ApiError)
  })
})
```

**Create file: `client/src/components/ApiErrorToaster.test.tsx`**

```tsx
import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiGet } from '../api/client'
import { ApiErrorToaster } from './ApiErrorToaster'

function problemResponse(status: number, body: object) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('ApiErrorToaster', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows an error toast with the reference id when an API call fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        problemResponse(500, { status: 500, title: 'An unexpected error occurred.', correlationId: 'abc123' }),
      ),
    )
    render(<ApiErrorToaster />)

    await expect(apiGet('/api/anything')).rejects.toThrow('500')

    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
    expect(screen.getByText('Reference: abc123')).toBeInTheDocument()
  })

  it('shows a connection message when the server cannot be reached', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
    render(<ApiErrorToaster />)

    await expect(apiGet('/api/anything')).rejects.toThrow('network error')

    expect(
      await screen.findByText('Cannot reach the server. Check your connection and try again.'),
    ).toBeInTheDocument()
  })

  it('does not show a toast when the request is aborted on purpose', async () => {
    const controller = new AbortController()
    vi.stubGlobal(
      'fetch',
      vi.fn().mockImplementation(() => {
        controller.abort()
        return Promise.reject(new DOMException('Aborted', 'AbortError'))
      }),
    )
    render(<ApiErrorToaster />)

    await expect(apiGet('/api/anything', controller.signal)).rejects.toThrow('Aborted')

    expect(screen.queryByRole('listitem')).not.toBeInTheDocument()
  })
})
```

**Create file: `client/src/App.toast.test.tsx`** — separate file because `App.test.tsx` mocks `./api/health` for the whole file (line 6).

```tsx
import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

// No module mocks here: the real API client runs against a stubbed fetch,
// so this proves the whole path API failure → toast on the home page.
describe('App error toast', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows an error toast when the health call fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ status: 500, correlationId: 'home-1' }), {
          status: 500,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    )

    render(<App />)

    expect(await screen.findByText('unavailable')).toBeInTheDocument()
    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
    expect(screen.getByText('Reference: home-1')).toBeInTheDocument()
  })
})
```

Run `npm test` → **Red** (imports `./errors`, `onApiError`, `./ApiErrorToaster` do not exist; the App toast test cannot find the toast).

### 3 — `ApiError` type (Green, part 1)

**Create file: `client/src/api/errors.ts`** — fields declared explicitly because of `erasableSyntaxOnly` (no constructor parameter properties).

```ts
/** RFC 7807 body returned by the API for every error (see server/src/Crm.Api/ErrorHandling). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  /** Field name (camelCase) → messages. Present on 400 validation errors. */
  errors?: Record<string, string[]>
  correlationId?: string
  traceId?: string
}

/** Thrown by the API client for every failed call. `status` is 0 when the server could not be reached. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | undefined
  readonly correlationId: string | undefined

  constructor(message: string, status: number, problem?: ProblemDetails, correlationId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
    this.correlationId = correlationId ?? problem?.correlationId
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}
```

### 4 — API client with error notification (Green, part 2)

**File: `client/src/api/client.ts`** — replace the whole file (7 lines). Still the **only** place that calls `fetch`. Error message format stays `"<METHOD> <path> failed with status <n>"` (existing `health.test.ts` asserts `'503'`).

```ts
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

async function request<T>(method: string, path: string, signal?: AbortSignal): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, { method, headers: { Accept: 'application/json' }, signal })
  } catch (error) {
    // Cancelled on purpose (component unmounted): not a failure the user must see.
    if (signal?.aborted) throw error
    return fail(new ApiError(`${method} ${path} failed: network error`, 0))
  }

  if (!response.ok) {
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

  return (await response.json()) as T
}

export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('GET', path, signal)
}
```

CRM-2 adds `apiPost` / `apiPut` / `apiDelete` (and the bearer token) on top of the same private `request` function — every verb then gets ProblemDetails parsing and toasts for free.

### 5 — Toast messages + toaster component (Green, part 3)

**Create file: `client/src/api/error-messages.ts`**

```ts
import type { ApiError } from './errors'

// Temporary English text. CRM-4 moves these strings to client/src/i18n/{en,ar}.json
// and replaces this module with translation keys.
const messages = {
  network: 'Cannot reach the server. Check your connection and try again.',
  badRequest: 'The request is invalid. Check the entered data.',
  forbidden: 'You do not have permission to do this.',
  notFound: 'The requested item was not found.',
  conflict: 'This change conflicts with existing data.',
  generic: 'Something went wrong. Please try again.',
  reference: (id: string) => `Reference: ${id}`,
}

export function getApiErrorMessage(error: ApiError): string {
  switch (error.status) {
    case 0:
      return messages.network
    case 400:
      return messages.badRequest
    case 403:
      return messages.forbidden
    case 404:
      return messages.notFound
    case 409:
      return messages.conflict
    default:
      return messages.generic
  }
}

export function getApiErrorDescription(error: ApiError): string | undefined {
  return error.correlationId ? messages.reference(error.correlationId) : undefined
}
```

The client chooses the text by **status**, not the server `title`: server titles are English-only, and CRM-4 must be able to translate every toast.

**Create file: `client/src/components/ApiErrorToaster.tsx`**

```tsx
import { useEffect } from 'react'
import { Toaster, toast } from 'sonner'
import { onApiError } from '../api/client'
import { getApiErrorDescription, getApiErrorMessage } from '../api/error-messages'

/**
 * Mount once at the app root: renders the toast container and shows an error toast
 * for every failed API call. CRM-3 swaps sonner's <Toaster> for the shadcn wrapper
 * (client/src/components/ui/sonner.tsx); the toast() calls stay the same.
 */
export function ApiErrorToaster() {
  useEffect(
    () =>
      onApiError((error) => {
        toast.error(getApiErrorMessage(error), { description: getApiErrorDescription(error) })
      }),
    [],
  )

  return <Toaster position="top-center" closeButton />
}
```

**Do not** pass `richColors` or any color props (theme colors come from CSS variables once CRM-3 adds the shadcn wrapper).

### 6 — Mount the toaster (Green, part 4)

**File: `client/src/App.tsx`** — add the import after line 2 and wrap the returned markup (lines 21–27) in a fragment with the toaster **next to** `<main>`:

```tsx
import { ApiErrorToaster } from './components/ApiErrorToaster'
```

```tsx
  // Temporary placeholder text: i18n arrives in CRM-4, layout in CRM-3.
  return (
    <>
      <main>
        <h1>Customer Support CRM</h1>
        <p>
          API status: <strong>{apiStatus}</strong>
        </p>
      </main>
      <ApiErrorToaster />
    </>
  )
```

The `useEffect` in `App` (lines 9–18) stays unchanged. Child effects run before parent effects, so the toaster subscribes before the health request starts.

Run `npm test` → **Green**: **11 passed** (2 `health.test.ts`, 2 `App.test.tsx`, 3 `client.test.ts`, 3 `ApiErrorToaster.test.tsx`, 1 `App.toast.test.tsx`).

### 7 — How CRM-3 builds on this (write nothing here; for the CRM-3 planner)

- CRM-3 runs `npx shadcn@latest add sonner` after `shadcn init`; it creates `client/src/components/ui/sonner.tsx` (wrapper over `sonner` + `next-themes`, colors from theme CSS variables).
- CRM-3 then changes **one import** in `client/src/components/ApiErrorToaster.tsx`: `Toaster` from `@/components/ui/sonner` instead of `sonner`. `toast` stays imported from `sonner`. CRM-3 **must not** reinstall `sonner` or add another toast mechanism.
- CRM-4 replaces `client/src/api/error-messages.ts` strings with i18n keys and passes `dir` from the current language to the `Toaster` (sonner supports `dir="rtl"`).

---

## Edge Cases & Failure Modes

- **FluentValidation vs. our `ValidationException` name clash** → compile error `CS0104 ambiguous reference`. Fix with the `using ValidationException = Crm.Application.Common.Exceptions.ValidationException;` alias (in `ValidatorExtensions.cs` and `ValidatorExtensionsTests.cs`).
- **Nested / collection properties** (`Address.City`, `Items[0].Name`) → field keys become `address.city`, `items[0].name` (`ToCamelCasePath` in `ValidatorExtensions.cs`). Covered by `ValidateOrThrowAsync_InvalidInstance_ThrowsWithCamelCaseFieldErrors`.
- **Several rules fail on one field** → one key, several messages (`GroupBy`). Covered by `ValidateOrThrowAsync_SeveralFailuresOnOneField_GroupsThemUnderOneKey`.
- **Malformed JSON body** → in `Testing`/`Production` minimal APIs write an empty 400 and `UseStatusCodePages()` turns it into ProblemDetails; in `Development` they throw `BadHttpRequestException`, mapped by `GlobalExceptionHandler`. Both covered (`MalformedJson_*`).
- **Exception message leaks secrets** (connection strings, SQL) → for 500 the handler never sets `Detail` outside Development (`GlobalExceptionHandler.TryHandleAsync`). Covered by `UnhandledException_InProduction_Returns500ProblemDetailsWithoutStackTrace`. 404/409/403 **do** expose the exception message — later stories write those messages for users.
- **Malicious `X-Correlation-Id`** (very long, newlines, log-injection) → rejected by the `^[A-Za-z0-9._-]{1,64}$` regex and replaced by a new GUID (`CorrelationIdMiddleware.ReadValidHeader`). Covered by `InvalidCorrelationHeader_IsReplacedByAGeneratedOne`.
- **Response already started when an exception is thrown** (e.g. streaming) → `UseExceptionHandler` cannot rewrite it and rethrows; the connection is aborted. Accepted; no streaming endpoints exist.
- **Double logging** → .NET 10's exception-handler middleware does not log exceptions an `IExceptionHandler` handled, so `GlobalExceptionHandler` logs them itself. `UnhandledException_IsLoggedWithCorrelationId` uses `Assert.Single` — if a framework update starts logging again, this test fails and shows it.
- **Console logs without scopes** → the default console logger does not print scopes, which is why `CorrelationId` is also in the message template of both log calls.
- **`CrmApiFactory` shared by test classes** → each `IClassFixture<CrmApiFactory>` gets its own instance; logs are filtered by a unique correlation id so tests do not see each other's entries.
- **Test-only endpoints leaking to production** → they are registered only by `TestEndpointsStartupFilter` in the test project; `Program.cs` has no reference to them.
- **Request aborted on unmount** (React StrictMode double-effect in dev) → `request` rethrows the abort error without notifying listeners: no toast (`client.ts`, catch block). Covered by `does not show a toast when the request is aborted on purpose`.
- **Server unreachable / Vite proxy error** → `fetch` rejects (status 0, "network" message) or the proxy returns 5xx without JSON (`readProblem` returns `undefined`, generic message). Covered by the connection test and `throws ApiError without a body when the response is not JSON`.
- **Toast spam** (many calls fail at once) → sonner shows at most 3 toasts at a time by default (`visibleToasts`). No dedupe in this story.
- **Toaster mounted twice** → every failure shows two toasts. Mount `ApiErrorToaster` only in `App.tsx`.
- **401 later (CRM-2)** → will also toast through `onApiError`; CRM-2 decides whether to skip 401 (redirect to login instead).

---

## Test Plan

1. **Unit** — `server/tests/Crm.UnitTests/Common/ValidatorExtensionsTests.cs`: `ValidateOrThrowAsync_ValidInstance_DoesNotThrow`, `ValidateOrThrowAsync_InvalidInstance_ThrowsWithCamelCaseFieldErrors` (AC 1), `ValidateOrThrowAsync_SeveralFailuresOnOneField_GroupsThemUnderOneKey`, `ApplicationExceptions_KeepTheirMessage`.
2. **Integration** — `server/tests/Crm.Api.IntegrationTests/ErrorHandling/ErrorHandlingTests.cs`: `InvalidRequest_Returns400ProblemDetailsWithFieldErrors` (AC 1), `ValidRequest_PassesValidation`, `MalformedJson_Returns400ProblemDetails`, `MalformedJson_InDevelopment_Returns400ProblemDetails`, `UnhandledException_InProduction_Returns500ProblemDetailsWithoutStackTrace` (AC 2), `UnhandledException_InDevelopment_IncludesExceptionDetail`, `KnownFailures_ReturnMatchingProblemDetails` ×4 (404/409/403/unknown route).
3. **Integration** — `server/tests/Crm.Api.IntegrationTests/ErrorHandling/CorrelationIdTests.cs`: `UnhandledException_IsLoggedWithCorrelationId` (AC 4), `MissingCorrelationHeader_GeneratesOne`, `InvalidCorrelationHeader_IsReplacedByAGeneratedOne`.
4. **Integration (modified)** — `server/tests/Crm.Api.IntegrationTests/HealthEndpointTests.cs`: now uses `CrmApiFactory`; still `GetHealth_Returns200WithStatusOk`.
5. **Unit (frontend)** — `client/src/api/client.test.ts`: ProblemDetails parsed into `ApiError`; non-JSON error body; listener notified once and unsubscribed.
6. **Component** — `client/src/components/ApiErrorToaster.test.tsx`: toast + reference on 500 (AC 3); network-failure message; no toast on abort.
7. **Component (app level)** — `client/src/App.toast.test.tsx`: home page shows "unavailable" **and** the error toast when `/api/health` fails (AC 3).
8. **Unchanged, must stay green** — `client/src/api/health.test.ts`, `client/src/App.test.tsx`, `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs` (proves Application still has no ASP.NET reference).
9. **Manual smoke** — Verification step 6.

---

## Verification Steps

1. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings.
2. **Backend tests:** in `server/` run `dotnet test` — **20 passed** (6 unit, 14 integration), 0 failed.
3. **Frontend tests:** in `client/` run `npm test` — **11 passed** in 5 files, process exits.
4. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed.
5. **Frontend lint:** in `client/` run `npm run lint` — oxlint reports no errors.
6. **Manual smoke:**
   - Terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http`.
   - `curl -i http://localhost:5080/api/nope -H "X-Correlation-Id: smoke-1"` → `404`, `Content-Type: application/problem+json`, header `X-Correlation-Id: smoke-1`, body contains `"correlationId":"smoke-1"`.
   - Terminal 2: `cd client && npm run dev`, open `http://localhost:5173` → "API status: ok", no toast. Stop the API and reload → "unavailable" **and** a toast "Something went wrong. Please try again." (the Vite proxy answers 5xx without JSON, so no Reference line) at the top center.
7. **Regression:** `git status` shows no changes under `.claude/`, `.mcp.json`, `CLAUDE.md`, `client/src/index.css`.

---

## Done Criteria

- [ ] Invalid request → 400 `application/problem+json` with `errors` keyed by camelCase field name (`InvalidRequest_Returns400ProblemDetailsWithFieldErrors` green).
- [ ] Unhandled exception → 500 ProblemDetails, no message / stack trace in Production (`UnhandledException_InProduction_…` green).
- [ ] A failed API call shows an error toast with a reference id (`ApiErrorToaster.test.tsx`, `App.toast.test.tsx` green).
- [ ] Errors are logged with the correlation id; `X-Correlation-Id` is echoed and present in every ProblemDetails body (`CorrelationIdTests` green).
- [ ] `ValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException` exist in `server/src/Crm.Application/Common/Exceptions/` and map to 400/404/409/403.
- [ ] `CrmApiFactory` exists in `server/tests/Crm.Api.IntegrationTests/Infrastructure/` and every integration test uses it.
- [ ] `sonner` is the only new npm dependency; no Tailwind / shadcn yet; `client/src/api/client.ts` is still the only `fetch` caller.
- [ ] `dotnet test` (20) and `npm test` (11) pass; `npm run build` and `npm run lint` pass.
- [ ] Committed on `feature/crm-5-error-handling` with message `CRM-5: global error handling and validation`.
- [ ] Overview `00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 03.**
