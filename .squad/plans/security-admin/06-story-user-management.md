# Story 06 — User management (Story: CRM-6)

## Prerequisites

- Foundation feature completed and merged to `main` ([../foundation/00-overview.md](../foundation/00-overview.md)):
  - Story 02 [../foundation/02-story-global-error-handling.md](../foundation/02-story-global-error-handling.md) (CRM-5) — ProblemDetails, `ValidationException` / `NotFoundException` / `ConflictException` / `ForbiddenException`, `ValidateOrThrowAsync`, `CrmApiFactory`.
  - Story 03 [../foundation/03-story-authentication.md](../foundation/03-story-authentication.md) (CRM-2) — `ApplicationUser`, `CrmDbContext`, migration `InitialIdentity`, seeded roles + `admin@crm.local`, `AuthService`, JWT (`role` claim, `RoleClaimType = "role"`). Its "6 — How later stories build on this" is **binding**: users are managed through `UserManager<ApplicationUser>` in Infrastructure behind an Application interface, a new column comes with a **new migration**, and `AuthService.LoginAsync` must reject inactive users with the **same** 401 message.
  - Story 04 [../foundation/04-story-app-layout.md](../foundation/04-story-app-layout.md) (CRM-3) — routes, `ComingSoonPage`, React Query, `client/src/test/fake-api.ts`, shadcn CLI pinned to **`shadcn@4.21.2`**. Its section 9 is binding: "CRM-6 (users): replaces the `users` coming-soon route with the user-management pages".
  - Story 05 [../foundation/05-story-i18n-rtl.md](../foundation/05-story-i18n-rtl.md) (CRM-4) — every UI string in `client/src/i18n/{en,ar}.json`, server texts in a static `<Feature>Text` class, guards `translations.test.ts`, `no-hardcoded-text.test.ts`, `LocalizedTextCatalogTests`. Its section 9 is binding (strings in both files, `createXSchema(t)` + `useMemo`, server field errors shown as-is).
- Work on branch **`feature/crm-6-user-management`** (already created from `main`).
- Phase 1 order: CRM-1 ✅ → CRM-5 ✅ → CRM-2 ✅ → CRM-3 ✅ → CRM-4 ✅ → **CRM-6 (this)** → CRM-7 (roles & permissions) → customers → tickets → SLA → email/WhatsApp.
- **dotnet-ef 10.0.8** installed globally (prints the harmless "older than runtime 10.0.11" warning). SQL Server LocalDB only for the manual smoke test.
- **Shared contract created here** (later stories reuse it): `PagedResult<T>` + `PagingDefaults`, `ICurrentUser`, `CrmPolicies` (policy names; CRM-7 changes only their definitions), the "inactive user's token is rejected" rule (`IActiveUserChecker`), `CrmApiFactory.CreateUserAsync` / `CreateClientWithRoleAsync`, client `apiPut`, `client/src/api/users.ts`, and the `ResizeObserver` stub in `client/src/test/setup.ts`.

---

## Story Goal

Admins (roles **SuperAdmin** and **Admin**) create, edit, deactivate and reactivate staff users from a "Users" page; nobody else can use the users API.

1. `POST /api/users` creates a user (email, full name, password, roles) → **201** + `Location`; the new user can log in (AC 1).
2. An email already used by another user (case-insensitive) → **400** ProblemDetails with `errors.email` = "This email is already used by another user." (AC 2). Invalid fields → 400 with field errors.
3. `POST /api/users/{id}/deactivate` → **204**; a deactivated user's login → **401** "Invalid email or password." (AC 3), and **tokens issued before the deactivation stop working** (401 on the next request). `POST /api/users/{id}/reactivate` → 204, login works again.
4. Any `/api/users` call by an authenticated user who is not SuperAdmin/Admin → **403** ProblemDetails; without a token → 401 (AC 4).
5. `GET /api/users?search=&page=&pageSize=` → `PagedResult<UserResponse>`; `search` matches full name **or** email (case-insensitive, `%`/`_` taken literally), ordered by full name then email; `page` default 1, `pageSize` default 20, max 100, out of range → 400 (AC 5). `GET /api/users/{id}` and `PUT /api/users/{id}` (email, full name, roles) complete the CRUD.
6. Two safety rules: you cannot deactivate **your own** account (409), and only a **SuperAdmin** may give the SuperAdmin role or change / deactivate a SuperAdmin (403 for an Admin).
7. Client: `/users` shows the real page (search, paged table, "Add user" / "Edit" dialog, "Deactivate" with confirmation, "Reactivate"), all text in English and Arabic.

**Decisions**

- **Deactivation = `ApplicationUser.IsActive` (bool, default `true`)**, new migration `AddUserIsActive`. No delete endpoint (deactivation keeps history for tickets later).
- **Existing tokens:** `JwtBearerEvents.OnTokenValidated` asks `IActiveUserChecker` (one indexed primary-key lookup per authenticated request) and fails the authentication when the user is inactive or deleted → 401. Chosen over a security-stamp claim (the Identity security stamp also keys TOTP tokens and should not travel in a readable JWT) and over waiting for token expiry (up to 60 minutes of access after deactivation).
- **Role changes are not pushed into existing tokens**: the `role` claims stay as issued until the next login (≤ 60 min). Deactivation is the immediate kill switch.
- **Authorization now = role-based named policy** `CrmPolicies.ManageUsers` → `RequireRole("SuperAdmin", "Admin")`, defined once in `AddCrmAuthentication`. Endpoints only reference the name, so **CRM-7 switches to permissions by changing the policy definition**, not the endpoints.
- **Duplicate email → 400 (AC 2), not 409**: a `ValidationException` with the `email` field, so the client shows it next to the field.
- **Password rules** are checked by `CreateUserRequestValidator` (translated message) **and** again by Identity (8+ chars, upper, lower, digit, symbol — `AddInfrastructure`). Leftover Identity errors (e.g. characters Identity does not allow in user names) become a 400 with English Identity text on `password` / `email`.
- **No password change in edit** (out of scope); the edit dialog has no password field.

**Not in scope:** permission policies, hiding the "Users" menu item / route for non-admins, hiding the SuperAdmin role option for Admins in the dialog (CRM-7); password reset/change, invitations, email confirmation, 2FA; deleting users; audit log; per-user language.

---

## Context — Read These Files First

1. `CLAUDE.md` — **Architecture decisions** (binding): *Endpoints* (`Crm.Api/Endpoints/<Feature>Endpoints.cs`, `/api/<resource>`), *Application layer* (`I<Feature>Service`, DTO records, FluentValidation, Application exceptions), *Pagination* (`page` default 1, `pageSize` default 20 max 100 → `PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`), *Persistence* (Identity types only in Infrastructure, migrations in `Crm.Infrastructure/Persistence/Migrations`), *Integration tests*. Backend rules: authorization on the API, `CancellationToken` passed through. Frontend rules: shadcn/ui only, theme colors, logical classes, all strings in `ar` + `en`, API only via `client/src/api`, tests by role/label.
2. `.squad/stories/security-admin/CRM-6/intake.md` — acceptance criteria 1–5 and **Out of scope**.
3. [../foundation/03-story-authentication.md](../foundation/03-story-authentication.md) lines 1833–1843 ("How later stories build on this": CRM-6, CRM-7 bullets, "tests that need another role create a user through `UserManager<ApplicationUser>`"), lines 1162–1170 (exact `dotnet ef migrations add` command). [../foundation/04-story-app-layout.md](../foundation/04-story-app-layout.md) lines 1286–1296. [../foundation/05-story-i18n-rtl.md](../foundation/05-story-i18n-rtl.md) lines 1716–1726 and the Edge Case "Quoted UI string in a code comment" (line 1740).
4. `server/src/Crm.Infrastructure/Identity/ApplicationUser.cs` — whole file (9 lines); you add `IsActive` after line 8.
5. `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` — lines 14–17 `builder.Entity<ApplicationUser>(…)`: add the `IsActive` mapping after line 16.
6. `server/src/Crm.Infrastructure/Identity/AuthService.cs` — line 19 `if (user is null || await userManager.IsLockedOutAsync(user))`: add `!user.IsActive`.
7. `server/src/Crm.Infrastructure/DependencyInjection.cs` — line 1 usings, lines 24–32 Identity options (password policy the validator mirrors: `RequiredLength = 8` + Identity defaults), line 34 `AddScoped<IAuthService, AuthService>()` (register the new services after it).
8. `server/src/Crm.Api/Auth/AuthenticationExtensions.cs` — whole file (53 lines). Lines 23–42 JwtBearer options (you add `Events` after line 41), line 44 `services.AddAuthorization();` (replaced by a policy builder).
9. `server/src/Crm.Api/Auth/JwtAccessTokenGenerator.cs` — lines 42–49 `AuthClaimTypes` (`UserId = "sub"`, `Role = "role"`), used by `HttpCurrentUser` and the token check.
10. `server/src/Crm.Api/Endpoints/AuthEndpoints.cs` — whole file (28 lines): the endpoint style to copy (group, `CancellationToken`, `.WithName`).
11. `server/src/Crm.Api/Program.cs` — line 32 `app.MapAuthEndpoints();` (add `app.MapUsersEndpoints();` after it).
12. `server/src/Crm.Api/ErrorHandling/GlobalExceptionHandler.cs` lines 51–93 and `ErrorHandlingExtensions.cs` lines 21–31 — already map `ValidationException` 400, `NotFoundException` 404, `ConflictException` 409, `ForbiddenException` 403 and the empty 401/403 written by authentication/authorization (title `ErrorText.Forbidden`). **No change.**
13. `server/src/Crm.Application/Auth/AuthText.cs`, `Auth/LoginRequestValidator.cs`, `Auth/Roles.cs` — text class, validator (`.WithName(_ => …)`), role constants (`Roles.All`). `UserText` and the user validators follow them.
14. `server/src/Crm.Application/Common/Validation/ValidatorExtensions.cs` — `ValidateOrThrowAsync` (camelCase field keys).
15. `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` — whole file (93 lines): lines 1–14 usings, line 28 `JwtSigningKey`, lines 66–81 `LoginAsync` / `CreateAuthenticatedClient` (the new helpers go between them).
16. `server/tests/Crm.Api.IntegrationTests/Auth/LoginTests.cs` lines 83–105 (`Login_AfterFiveWrongPasswords_…`) — creating a user through `UserManager` in a scope (pattern of `CreateUserAsync`).
17. `server/tests/Crm.UnitTests/Localization/LocalizedTextCatalogTests.cs` — lines 3–4 usings, lines 36–41 `Catalog_FindsTheTextClasses` (add `UserText`). Every `UserText` property is tested automatically.
18. `server/tests/Crm.UnitTests/Auth/LoginRequestValidatorTests.cs` — unit-test style incl. `UiCulture.Use("ar", …)`.
19. `client/src/api/client.ts` — lines 79–85 `apiGet` / `apiPost` (add `apiPut` after line 85). Line 75: a 204 returns `undefined`.
20. `client/src/api/auth.ts`, `client/src/features/auth/LoginForm.tsx`, `client/src/features/auth/login-schema.ts` — typed API module, react-hook-form + zod + `Controller` + `Field`/`FieldError` pattern, schema factory taking `t`.
21. `client/src/app/AppRoutes.tsx` — line 5 imports, line 20 `users` coming-soon route (replace).
22. `client/src/pages/dashboard/DashboardPage.tsx` + `DashboardPage.test.tsx` — page layout (`h1` `text-2xl font-semibold`) and the page-test pattern (`QueryClientProvider client={createQueryClient()}`, `vi.mock('@/api/…')`).
23. `client/src/test/fake-api.ts` lines 23–42 (`fakeApi`; add `/api/users`), `client/src/test/setup.ts` lines 6–21 (`matchMedia` stub; add `ResizeObserver` stub after it).
24. `client/src/App.layout.test.tsx` — line 7 `NAVIGATION_LABELS`, lines 119–132 "opens a page for every navigation item" (Users is no longer "coming soon").
25. `client/src/i18n/en.json` / `ar.json` — lines 50–53 `toast` block (add `users` after it); `client/src/no-hardcoded-text.test.ts` lines 52–61 (English values > 3 chars must not appear quoted in code — **including comments**).
26. `client/src/components/ApiErrorToaster.tsx` — every failed call toasts (401 excepted); `toast.success` from `sonner` renders in the same `Toaster`.
27. `.claude/skills/vercel-react-best-practices/SKILL.md` — direct imports (no barrel files).

Verified while planning (scratch clone of this branch in the session scratchpad; every snippet below compiled and ran): **`dotnet build` 0 warnings / 0 errors; `dotnet test` 141 passed (62 unit, 79 integration); `npm test` 364 passed in 18 files; `npm run build` OK (Vite chunk-size warning only); `npm run lint` exit 0.** Further findings:

- `HasDefaultValue(true)` alone on a `bool` makes EF treat `false` as "not set" (sentinel) on insert; `.ValueGeneratedNever()` keeps the column default for **existing rows** (migration `defaultValue: true`) while EF always writes the property. Verified on LocalDB: a row inserted before `AddUserIsActive` got `IsActive = 1`; `Down` drops the column.
- `[AsParameters] ListUsersQuery` (positional record from Crm.Application) binds `search`, `page`, `pageSize` from the query string; `MapGet("")` on the group serves `/api/users`.
- `EF.Functions.Like(value, pattern, "\\")` works on SQLite (tests) and SQL Server; SQLite `LIKE` is case-insensitive for ASCII, SQL Server's default collation is case-insensitive.
- Without the `OnTokenValidated` check, `DeactivatedUser_ExistingToken_StopsWorking` fails (`Expected: Unauthorized, Actual: OK`) — proven by removing the line.
- `npx shadcn@4.21.2 add table dialog checkbox badge alert-dialog -y` creates 5 files, skips the identical `button.tsx`, adds **no** npm packages (they use the existing `radix-ui` package). The generated dialog close button has an English sr-only "Close" → the plan uses `showCloseButton={false}` + a translated "Cancel" button.
- Radix `Checkbox` inside a `<form>` calls `ResizeObserver` → jsdom throws `ReferenceError: ResizeObserver is not defined` until the stub is added to `client/src/test/setup.ts`.
- A JSDoc comment containing `"Deactivate"` / `"Reactivate"` in quotes failed `no-hardcoded-text.test.ts` → comments must not quote UI strings.
- HTTP smoke on a throw-away LocalDB database: create → 201, duplicate email with `Accept-Language: ar` → 400 Arabic message, search → 1 item, Agent → 403, deactivate → 204, agent's old token → 401, agent login → 401.

---

## Backend Tasks

All commands run from `server/`. **No new NuGet packages.**

### 1 — Unit tests first (Red)

**Create file: `server/tests/Crm.UnitTests/Users/UserRequestValidatorTests.cs`**

```csharp
using Crm.Application.Users;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Users;

public class UserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _create = new();
    private readonly UpdateUserRequestValidator _update = new();
    private readonly ListUsersQueryValidator _list = new();

    private static CreateUserRequest ValidCreate() =>
        new("agent@crm.local", "Sara Agent", "Agent#Pass1", ["Agent"]);

    [Fact]
    public void ValidCreateRequest_HasNoErrors()
    {
        Assert.True(_create.Validate(ValidCreate()).IsValid);
    }

    [Theory]
    [InlineData(null, "Email")]
    [InlineData("not-an-email", "Email")]
    public void CreateRequest_WithBadEmail_ReportsEmail(string? email, string field)
    {
        var result = _create.Validate(ValidCreate() with { Email = email });

        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void CreateRequest_WithoutFullName_ReportsFullName(string? fullName)
    {
        var result = _create.Validate(ValidCreate() with { FullName = fullName });

        Assert.Contains(result.Errors, e => e.PropertyName == "FullName");
    }

    [Theory]
    [InlineData("Short#1")] // 7 characters
    [InlineData("alllower#1")]
    [InlineData("ALLUPPER#1")]
    [InlineData("NoDigits#x")]
    [InlineData("NoSymbol12")]
    public void CreateRequest_WithWeakPassword_ReportsPassword(string password)
    {
        var result = _create.Validate(ValidCreate() with { Password = password });

        var error = Assert.Single(result.Errors);
        Assert.Equal("Password", error.PropertyName);
    }

    [Fact]
    public void CreateRequest_WithoutRoles_ReportsRoles()
    {
        var result = _create.Validate(ValidCreate() with { Roles = [] });

        Assert.Contains(result.Errors, e => e.PropertyName == "Roles");
    }

    [Fact]
    public void CreateRequest_WithUnknownRole_ReportsRoles()
    {
        var result = _create.Validate(ValidCreate() with { Roles = ["Agent", "admin"] }); // role names are case-sensitive

        Assert.Contains(result.Errors, e => e.PropertyName == "Roles");
    }

    [Fact]
    public void CreateRequest_InArabic_HasArabicMessages()
    {
        var messages = UiCulture.Use("ar", () => _create
            .Validate(new CreateUserRequest("", "", "weak", []))
            .Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("'الاسم الكامل' لا يجب أن يكون فارغاً.", messages);
        Assert.Contains("اختر دوراً واحداً على الأقل.", messages);
        Assert.Contains("يجب أن تتكون كلمة المرور من 8 أحرف على الأقل وتحتوي على حرف كبير وحرف صغير ورقم ورمز.", messages);
    }

    [Fact]
    public void UpdateRequest_ValidAndInvalid()
    {
        Assert.True(_update.Validate(new UpdateUserRequest("agent@crm.local", "Sara", ["Supervisor"])).IsValid);

        var result = _update.Validate(new UpdateUserRequest("bad", "", null));

        Assert.Equal(["Email", "FullName", "Roles"], result.Errors.Select(e => e.PropertyName).Distinct().Order());
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void ListQuery_PagingLimits(int? page, int? pageSize, bool valid)
    {
        Assert.Equal(valid, _list.Validate(new ListUsersQuery(null, page, pageSize)).IsValid);
    }
}
```

**File: `server/tests/Crm.UnitTests/Localization/LocalizedTextCatalogTests.cs`** — after line 4 (`using Crm.Application.Common.Localization;`) add `using Crm.Application.Users;`, and after line 40 (`Assert.Contains(typeof(ErrorText), TextClasses);`) add:

```csharp
        Assert.Contains(typeof(UserText), TextClasses);
```

Run `dotnet test` → **Red** (compile errors: namespace `Crm.Application.Users` does not exist).

### 2 — Application: paging, current user, active-user check, users contracts (Green for unit tests)

**Create file: `server/src/Crm.Application/Common/Paging/PagedResult.cs`**

```csharp
namespace Crm.Application.Common.Paging;

/// <summary>One page of a list (CLAUDE.md "Pagination"). <c>TotalCount</c> counts every match, not only this page.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Query-string defaults and limits shared by every paged list endpoint.</summary>
public static class PagingDefaults
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
```

(The class is **not** called `Paging`: it would have the same name as its namespace.)

**Create file: `server/src/Crm.Application/Common/Security/ICurrentUser.cs`**

```csharp
namespace Crm.Application.Common.Security;

/// <summary>The signed-in user of the current request (implemented in Crm.Api from the JWT claims).</summary>
public interface ICurrentUser
{
    /// <summary>The user's id (<c>sub</c> claim), or null when the request is anonymous.</summary>
    Guid? UserId { get; }

    bool IsInRole(string role);
}
```

**Create file: `server/src/Crm.Application/Auth/IActiveUserChecker.cs`**

```csharp
namespace Crm.Application.Auth;

/// <summary>
/// Checked on every authenticated request (JWT <c>OnTokenValidated</c>): a token of a deactivated or deleted
/// user stops working immediately instead of at its expiry.
/// </summary>
public interface IActiveUserChecker
{
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken);
}
```

**Create file: `server/src/Crm.Application/Users/UserContracts.cs`**

```csharp
namespace Crm.Application.Users;

/// <summary>GET /api/users query string: <c>search</c> (name or email), <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record ListUsersQuery(string? Search, int? Page, int? PageSize);

public sealed record CreateUserRequest(string? Email, string? FullName, string? Password, IReadOnlyList<string>? Roles);

public sealed record UpdateUserRequest(string? Email, string? FullName, IReadOnlyList<string>? Roles);

public sealed record UserResponse(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, bool IsActive);
```

Request properties are **nullable** on purpose (same as `LoginRequest`): a missing field must reach the validator (400 with field errors), not fail JSON binding.

**Create file: `server/src/Crm.Application/Users/IUserService.cs`**

```csharp
using Crm.Application.Common.Paging;

namespace Crm.Application.Users;

/// <summary>
/// Staff user management (the API allows it only to SuperAdmin and Admin: policy <c>ManageUsers</c>).
/// Failures: <c>ValidationException</c> 400 (invalid data, email already in use), <c>NotFoundException</c> 404,
/// <c>ForbiddenException</c> 403 (SuperAdmin rule), <c>ConflictException</c> 409 (deactivating yourself).
/// </summary>
public interface IUserService
{
    Task<PagedResult<UserResponse>> ListAsync(ListUsersQuery query, CancellationToken cancellationToken);

    Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task ReactivateAsync(Guid id, CancellationToken cancellationToken);
}
```

**Create file: `server/src/Crm.Application/Users/UserText.cs`**

```csharp
using Crm.Application.Common.Localization;

namespace Crm.Application.Users;

/// <summary>User-facing text of the user-management feature, in the request language.</summary>
public static class UserText
{
    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string FullNameField => LocalizedText.Get("Full name", "الاسم الكامل");

    public static string PasswordField => LocalizedText.Get("Password", "كلمة المرور");

    public static string PageField => LocalizedText.Get("Page", "الصفحة");

    public static string PageSizeField => LocalizedText.Get("Page size", "حجم الصفحة");

    public static string EmailTaken => LocalizedText.Get(
        "This email is already used by another user.",
        "هذا البريد الإلكتروني مستخدم من قبل مستخدم آخر.");

    public static string WeakPassword => LocalizedText.Get(
        "Password must be at least 8 characters and contain an upper-case letter, a lower-case letter, a digit and a symbol.",
        "يجب أن تتكون كلمة المرور من 8 أحرف على الأقل وتحتوي على حرف كبير وحرف صغير ورقم ورمز.");

    public static string RolesRequired => LocalizedText.Get(
        "Select at least one role.",
        "اختر دوراً واحداً على الأقل.");

    public static string UnknownRole => LocalizedText.Get(
        "Unknown role.",
        "دور غير معروف.");

    public static string NotFound => LocalizedText.Get(
        "The user was not found.",
        "المستخدم غير موجود.");

    public static string CannotDeactivateSelf => LocalizedText.Get(
        "You cannot deactivate your own account.",
        "لا يمكنك إيقاف حسابك الخاص.");

    public static string SuperAdminOnly => LocalizedText.Get(
        "Only a super administrator can assign the SuperAdmin role or change a super administrator.",
        "لا يمكن إلا لمدير النظام منح دور مدير النظام أو تعديل حساب مدير نظام.");
}
```

**Create file: `server/src/Crm.Application/Users/UserRules.cs`**

```csharp
using Crm.Application.Auth;
using FluentValidation;

namespace Crm.Application.Users;

/// <summary>Field rules shared by the create and update validators.</summary>
internal static class UserRules
{
    public static void ValidEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(256).WithName(_ => UserText.EmailField);

    public static void ValidFullName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(200).WithName(_ => UserText.FullNameField);

    public static void ValidRoles<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule) =>
        rule.Must(roles => roles is { Count: > 0 }).WithMessage(_ => UserText.RolesRequired)
            .Must(roles => roles is null || roles.All(role => Roles.All.Contains(role)))
            .WithMessage(_ => UserText.UnknownRole);
}
```

**Create file: `server/src/Crm.Application/Users/CreateUserRequestValidator.cs`**

```csharp
using FluentValidation;

namespace Crm.Application.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FullName).ValidFullName();
        // Same policy as ASP.NET Identity in Crm.Infrastructure (8+ chars, upper, lower, digit, symbol), checked
        // here so the message is translated; Identity checks it again when the user is created.
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128).WithName(_ => UserText.PasswordField)
            .Must(BeStrongPassword).WithMessage(_ => UserText.WeakPassword);
        RuleFor(x => x.Roles).ValidRoles();
    }

    private static bool BeStrongPassword(string? password) =>
        string.IsNullOrEmpty(password) // reported by NotEmpty
        || (password.Length >= 8
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c)));
}
```

**Create file: `server/src/Crm.Application/Users/UpdateUserRequestValidator.cs`**

```csharp
using FluentValidation;

namespace Crm.Application.Users;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.Roles).ValidRoles();
    }
}
```

**Create file: `server/src/Crm.Application/Users/ListUsersQueryValidator.cs`**

```csharp
using Crm.Application.Common.Paging;
using FluentValidation;

namespace Crm.Application.Users;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithName(_ => UserText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => UserText.PageSizeField);
    }
}
```

The validators are registered automatically by `AddValidatorsFromAssembly` (`server/src/Crm.Application/DependencyInjection.cs` line 11). **Do not** add packages to `Crm.Application`.

Run `dotnet test` → unit tests **Green: 62 passed** (31 existing + 19 `UserRequestValidatorTests` + 12 new `TextProperty_HasEnglishAndArabicText` rows for `UserText`); integration **42 passed** (unchanged).

### 3 — Integration test host helpers + integration tests (Red)

**File: `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs`**

- Usings: add `using Crm.Infrastructure.Identity;` before line 3 and `using Microsoft.AspNetCore.Identity;` after line 4 (`using Microsoft.AspNetCore.Hosting;`).
- After line 28 (`JwtSigningKey`) add:

```csharp
    public const string TestUserPassword = "Test#User123";
```

- Between `LoginAsync` (ends line 73) and the `CreateAuthenticatedClient` doc comment (line 75) add:

```csharp
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

```

**Create file: `server/tests/Crm.Api.IntegrationTests/Users/UserBodies.cs`**

```csharp
namespace Crm.Api.IntegrationTests.Users;

/// <summary>JSON shapes of /api/users responses, as the client sees them.</summary>
public sealed record UserBody(Guid Id, string Email, string FullName, string[] Roles, bool IsActive);

public sealed record UserPageBody(UserBody[] Items, int Page, int PageSize, int TotalCount);
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Users/UserManagementTests.cs`** — AC 1, 2, 3 + edit / safety rules:

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Users;

public class UserManagementTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string UsersPath = "/api/users";
    private const string Password = "Agent#Pass123";

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@crm.local";

    private async Task<HttpClient> AdminClientAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    private static async Task<UserBody> CreateAsync(HttpClient admin, string email, params string[] roles)
    {
        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email, fullName = "New Agent", password = Password, roles });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserBody>())!;
    }

    private Task<HttpResponseMessage> LoginAsync(string email) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });

    [Fact]
    public async Task CreateUser_ThenTheNewUserCanLogIn()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();

        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email, fullName = "  Sara Agent  ", password = Password, roles = new[] { "Agent" } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserBody>();
        Assert.Equal($"/api/users/{user!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(email, user.Email);
        Assert.Equal("Sara Agent", user.FullName);
        Assert.Equal(["Agent"], user.Roles);
        Assert.True(user.IsActive);

        var login = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData(CrmApiFactory.SuperAdminEmail)]
    [InlineData("ADMIN@CRM.LOCAL")]
    public async Task CreateUser_WithExistingEmail_Returns400WithEmailError(string email)
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email, fullName = "Copy", password = Password, roles = new[] { "Agent" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["This email is already used by another user."], problem!.Errors["email"]);
    }

    [Fact]
    public async Task CreateUser_WithInvalidData_Returns400WithFieldErrors()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email = "not-an-email", fullName = "", password = "weak", roles = new[] { "Pilot" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["email", "fullName", "password", "roles"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetUser_ReturnsTheUser_AndUnknownIdReturns404()
    {
        var admin = await AdminClientAsync();
        var created = await CreateAsync(admin, NewEmail(), "Supervisor");

        var found = await admin.GetFromJsonAsync<UserBody>($"{UsersPath}/{created.Id}");
        var missing = await admin.GetAsync($"{UsersPath}/{Guid.NewGuid()}");

        Assert.Equal(created.Email, found!.Email);
        Assert.Equal(["Supervisor"], found.Roles);
        Assert.True(found.IsActive);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var problem = await missing.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("The user was not found.", problem!.Detail);
    }

    [Fact]
    public async Task UpdateUser_ChangesNameEmailAndRoles()
    {
        var admin = await AdminClientAsync();
        var created = await CreateAsync(admin, NewEmail(), "Agent");
        var newEmail = NewEmail();

        var response = await admin.PutAsJsonAsync($"{UsersPath}/{created.Id}",
            new { email = newEmail, fullName = "Renamed", roles = new[] { "Supervisor", "Agent" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await admin.GetFromJsonAsync<UserBody>($"{UsersPath}/{created.Id}");
        Assert.Equal(newEmail, updated!.Email);
        Assert.Equal("Renamed", updated.FullName);
        Assert.Equal(["Agent", "Supervisor"], updated.Roles);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(newEmail)).StatusCode);
    }

    [Fact]
    public async Task UpdateUser_WithEmailOfAnotherUser_Returns400()
    {
        var admin = await AdminClientAsync();
        var created = await CreateAsync(admin, NewEmail(), "Agent");

        var response = await admin.PutAsJsonAsync($"{UsersPath}/{created.Id}",
            new { email = CrmApiFactory.SuperAdminEmail, fullName = "Thief", roles = new[] { "Agent" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("email", problem!.Errors.Keys);
    }

    [Fact]
    public async Task UpdateUser_UnknownId_Returns404()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"{UsersPath}/{Guid.NewGuid()}",
            new { email = NewEmail(), fullName = "Nobody", roles = new[] { "Agent" } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_TryingToLogIn_Gets401()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var created = await CreateAsync(admin, email, "Agent");

        var deactivate = await admin.PostAsync($"{UsersPath}/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        var login = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var problem = await login.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", problem!.Detail);
        var user = await admin.GetFromJsonAsync<UserBody>($"{UsersPath}/{created.Id}");
        Assert.False(user!.IsActive);
    }

    [Fact]
    public async Task DeactivatedUser_ExistingToken_StopsWorking()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var created = await CreateAsync(admin, email, "Agent");
        var agent = factory.CreateAuthenticatedClient(await factory.LoginAsync(email, Password));
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/api/auth/me")).StatusCode);

        await admin.PostAsync($"{UsersPath}/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ReactivatedUser_CanLogInAgain()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var created = await CreateAsync(admin, email, "Agent");
        await admin.PostAsync($"{UsersPath}/{created.Id}/deactivate", null);

        var reactivate = await admin.PostAsync($"{UsersPath}/{created.Id}/reactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Deactivate_YourOwnAccount_Returns409()
    {
        var admin = await AdminClientAsync();
        var me = await admin.GetFromJsonAsync<UserBody>("/api/auth/me");

        var response = await admin.PostAsync($"{UsersPath}/{me!.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("You cannot deactivate your own account.", problem!.Detail);
    }

    [Fact]
    public async Task Admin_CannotCreateASuperAdmin_Returns403()
    {
        var admin = await factory.CreateClientWithRoleAsync("Admin");

        var response = await admin.PostAsJsonAsync(UsersPath,
            new { email = NewEmail(), fullName = "Escalation", password = Password, roles = new[] { "SuperAdmin" } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeactivateTheSuperAdmin_Returns403()
    {
        var superAdmin = await AdminClientAsync();
        var superAdminId = (await superAdmin.GetFromJsonAsync<UserBody>("/api/auth/me"))!.Id;
        var admin = await factory.CreateClientWithRoleAsync("Admin");

        var response = await admin.PostAsync($"{UsersPath}/{superAdminId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await superAdmin.GetAsync("/api/auth/me")).StatusCode);
    }
}
```

(`/api/auth/me` has no `isActive`; deserializing it into `UserBody` leaves `IsActive = false` — only `Id` is used.)

**Create file: `server/tests/Crm.Api.IntegrationTests/Users/UsersAuthorizationTests.cs`** — AC 4:

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Users;

public class UsersAuthorizationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Every /api/users endpoint (method, path). The ids do not need to exist: authorization runs first.</summary>
    public static TheoryData<string, string> Endpoints() => new()
    {
        { "GET", "/api/users" },
        { "GET", $"/api/users/{Guid.Empty}" },
        { "POST", "/api/users" },
        { "PUT", $"/api/users/{Guid.Empty}" },
        { "POST", $"/api/users/{Guid.Empty}/deactivate" },
        { "POST", $"/api/users/{Guid.Empty}/reactivate" },
    };

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        // A valid body, so a missing authorization check would show up as 201/200 instead of 403.
        Content = method is "POST" or "PUT"
            ? JsonContent.Create(new
            {
                email = $"blocked-{Guid.NewGuid():N}@crm.local",
                fullName = "Blocked",
                password = "Blocked#123",
                roles = new[] { "Agent" },
            })
            : null,
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task UsersApi_AsAgent_Returns403(string method, string path)
    {
        var agent = await factory.CreateClientWithRoleAsync("Agent");

        var response = await agent.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UsersApi_AsSupervisor_Returns403()
    {
        var supervisor = await factory.CreateClientWithRoleAsync("Supervisor");

        var response = await supervisor.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("You do not have permission to perform this action.", problem!.Title);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task UsersApi_WithoutToken_Returns401(string method, string path)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task UsersApi_AsAdminOrSuperAdmin_Returns200(string role)
    {
        var admin = await factory.CreateClientWithRoleAsync(role);

        var response = await admin.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Users/UserListTests.cs`** — AC 5. Each test creates its own users with a unique tag and searches for that tag, so tests in the class never see each other's data:

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Users;

public class UserListTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<HttpClient> AdminClientAsync() => factory.CreateAuthenticatedClient(await factory.LoginAsync());

    /// <summary>Creates users "&lt;tag&gt; 1", "&lt;tag&gt; 2", … with emails "&lt;tag&gt;-n@example.test" (unique per test).</summary>
    private async Task<string> CreateUsersAsync(int count)
    {
        var tag = $"t{Guid.NewGuid():N}"[..12];
        var admin = await AdminClientAsync();
        for (var n = 1; n <= count; n++)
        {
            var response = await admin.PostAsJsonAsync("/api/users", new
            {
                email = $"{tag}-{n}@example.test",
                fullName = $"{tag} {n}",
                password = "List#Pass123",
                roles = new[] { "Agent" },
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        return tag;
    }

    [Fact]
    public async Task List_WithoutParameters_ReturnsFirstPageOf20WithTotalCount()
    {
        var admin = await AdminClientAsync();

        var page = await admin.GetFromJsonAsync<UserPageBody>("/api/users");

        Assert.Equal(1, page!.Page);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.TotalCount >= 1);
        var superAdmin = Assert.Single(page.Items, u => u.Email == CrmApiFactory.SuperAdminEmail);
        Assert.Equal(["SuperAdmin"], superAdmin.Roles);
        Assert.True(superAdmin.IsActive);
    }

    [Fact]
    public async Task List_SearchByName_ReturnsOnlyMatchingUsers()
    {
        var tag = await CreateUsersAsync(3);
        var admin = await AdminClientAsync();

        var page = await admin.GetFromJsonAsync<UserPageBody>($"/api/users?search={tag} 2");

        var user = Assert.Single(page!.Items);
        Assert.Equal($"{tag} 2", user.FullName);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task List_SearchByEmail_IsCaseInsensitive()
    {
        var tag = await CreateUsersAsync(2);
        var admin = await AdminClientAsync();

        var page = await admin.GetFromJsonAsync<UserPageBody>($"/api/users?search={tag.ToUpperInvariant()}-1@EXAMPLE");

        Assert.Equal($"{tag}-1@example.test", Assert.Single(page!.Items).Email);
    }

    [Fact]
    public async Task List_SearchTreatsWildcardsAsText()
    {
        await CreateUsersAsync(1);
        var admin = await AdminClientAsync();

        var page = await admin.GetFromJsonAsync<UserPageBody>("/api/users?search=%25");

        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task List_Paginates_WithTotalCountOfAllMatches()
    {
        var tag = await CreateUsersAsync(3);
        var admin = await AdminClientAsync();

        var first = await admin.GetFromJsonAsync<UserPageBody>($"/api/users?search={tag}&page=1&pageSize=2");
        var second = await admin.GetFromJsonAsync<UserPageBody>($"/api/users?search={tag}&page=2&pageSize=2");

        Assert.Equal([$"{tag} 1", $"{tag} 2"], first!.Items.Select(u => u.FullName));
        Assert.Equal([$"{tag} 3"], second!.Items.Select(u => u.FullName));
        Assert.All([first, second], page => Assert.Equal(3, page.TotalCount));
        Assert.Equal(2, second.Page);
        Assert.Equal(2, second.PageSize);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task List_WithInvalidPaging_Returns400(string queryString, string field)
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync($"/api/users?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }
}
```

Run `dotnet test` → **Red**: the project compiles; the 37 new integration tests fail (`/api/users` does not exist → 404 instead of 201/200/400/401/403). Unit tests stay green (62).

### 4 — Infrastructure: `IsActive`, login check, user service

**File: `server/src/Crm.Infrastructure/Identity/ApplicationUser.cs`** — after line 8 (`FullName`) add:

```csharp

    /// <summary>False after an admin deactivated the user: login is refused and existing tokens stop working.</summary>
    public bool IsActive { get; set; } = true;
```

**File: `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — after line 16 (`FullName` mapping) add:

```csharp
            // Rows that exist when the column is added (e.g. the seeded SuperAdmin) become active.
            user.Property(u => u.IsActive).HasDefaultValue(true).ValueGeneratedNever();
```

`ValueGeneratedNever()` is required: without it EF treats `false` as "not set" on insert and lets the database default (`true`) win.

**File: `server/src/Crm.Infrastructure/Identity/AuthService.cs`** — line 19 becomes:

```csharp
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
```

(Same `AuthText.InvalidCredentials` 401 as a wrong password: the response never reveals that the account exists but is deactivated.)

**Create file: `server/src/Crm.Infrastructure/Identity/ActiveUserChecker.cs`**

```csharp
using Crm.Application.Auth;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Identity;

public sealed class ActiveUserChecker(CrmDbContext db) : IActiveUserChecker
{
    public Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
}
```

**Create file: `server/src/Crm.Infrastructure/Identity/UserService.cs`**

```csharp
using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Users;
using Crm.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Infrastructure.Identity;

/// <summary>User management on ASP.NET Identity (<see cref="UserManager{TUser}"/>) and <see cref="CrmDbContext"/>.</summary>
public sealed class UserService(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser,
    IValidator<ListUsersQuery> listValidator,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator) : IUserService
{
    private const string LikeEscape = "\\";

    public async Task<PagedResult<UserResponse>> ListAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        var page = query.Page ?? PagingDefaults.DefaultPage;
        var pageSize = query.PageSize ?? PagingDefaults.DefaultPageSize;

        var users = db.Users.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE is case-insensitive on SQL Server (default collation) and on SQLite (ASCII); % and _ are escaped.
            var pattern = $"%{EscapeLike(search)}%";
            users = users.Where(u => EF.Functions.Like(u.FullName, pattern, LikeEscape)
                                     || EF.Functions.Like(u.Email!, pattern, LikeEscape));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var pageUsers = await users
            .OrderBy(u => u.FullName).ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var roles = await RolesByUserAsync(pageUsers.Select(u => u.Id).ToList(), cancellationToken);
        var items = pageUsers
            .Select(u => ToResponse(u, roles.TryGetValue(u.Id, out var userRoles) ? userRoles : []))
            .ToList();
        return new PagedResult<UserResponse>(items, page, pageSize, totalCount);
    }

    public async Task<UserResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        return ToResponse(user, [.. await userManager.GetRolesAsync(user)]);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var roles = request.Roles!.Distinct(StringComparer.Ordinal).ToList();
        EnsureMayManage(roles);

        var email = request.Email!.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw EmailTaken();
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.FullName!.Trim(),
        };

        // One transaction: a user is never left without roles.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        ThrowIfFailed(await userManager.CreateAsync(user, request.Password!));
        ThrowIfFailed(await userManager.AddToRolesAsync(user, roles));
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(user, roles);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var user = await FindAsync(id);
        var currentRoles = await userManager.GetRolesAsync(user);
        var roles = request.Roles!.Distinct(StringComparer.Ordinal).ToList();
        EnsureMayManage(currentRoles);
        EnsureMayManage(roles);

        var email = request.Email!.Trim();
        var owner = await userManager.FindByEmailAsync(email);
        if (owner is not null && owner.Id != user.Id)
        {
            throw EmailTaken();
        }

        user.Email = email;
        user.UserName = email;
        user.FullName = request.FullName!.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // UpdateAsync also refreshes NormalizedEmail / NormalizedUserName.
        ThrowIfFailed(await userManager.UpdateAsync(user));
        ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, currentRoles.Except(roles)));
        ThrowIfFailed(await userManager.AddToRolesAsync(user, roles.Except(currentRoles)));
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(user, roles);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        if (user.Id == currentUser.UserId)
        {
            throw new ConflictException(UserText.CannotDeactivateSelf);
        }

        EnsureMayManage(await userManager.GetRolesAsync(user));
        await SetActiveAsync(user, false);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindAsync(id);
        EnsureMayManage(await userManager.GetRolesAsync(user));
        await SetActiveAsync(user, true);
    }

    private async Task SetActiveAsync(ApplicationUser user, bool isActive)
    {
        if (user.IsActive == isActive)
        {
            return; // Idempotent: deactivating an inactive user (or reactivating an active one) changes nothing.
        }

        user.IsActive = isActive;
        ThrowIfFailed(await userManager.UpdateAsync(user));
    }

    private async Task<ApplicationUser> FindAsync(Guid id) =>
        await userManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException(UserText.NotFound);

    private async Task<Dictionary<Guid, List<string>>> RolesByUserAsync(
        List<Guid> userIds, CancellationToken cancellationToken)
    {
        var pairs = await db.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(db.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(pair => pair.UserId)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.RoleName).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>Only a SuperAdmin may give the SuperAdmin role or change a SuperAdmin (no privilege escalation by an Admin).</summary>
    private void EnsureMayManage(IEnumerable<string> roles)
    {
        if (roles.Contains(Roles.SuperAdmin) && !currentUser.IsInRole(Roles.SuperAdmin))
        {
            throw new ForbiddenException(UserText.SuperAdminOnly);
        }
    }

    private static ValidationException EmailTaken() =>
        new(new Dictionary<string, string[]> { ["email"] = [UserText.EmailTaken] });

    /// <summary>Identity rejected the change (rules the validators do not cover, e.g. characters in the user name): 400.</summary>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email")
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
        throw new ValidationException(errors);
    }

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");

    private static UserResponse ToResponse(ApplicationUser user, IReadOnlyList<string> roles) =>
        new(user.Id, user.Email!, user.FullName, [.. roles.Order(StringComparer.Ordinal)], user.IsActive);
}
```

Notes: `UserManager` methods take no `CancellationToken` (Identity API); the token goes to the validators, EF queries and the transaction. The transaction works because `UserManager`'s store uses the same scoped `CrmDbContext`.

**File: `server/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 1 add `using Crm.Application.Users;`; after line 34 (`services.AddScoped<IAuthService, AuthService>();`) add:

```csharp
        services.AddScoped<IActiveUserChecker, ActiveUserChecker>();
        services.AddScoped<IUserService, UserService>();
```

### 5 — Api: current user, policy, token check, endpoints

**Create file: `server/src/Crm.Api/Auth/CrmPolicies.cs`**

```csharp
namespace Crm.Api.Auth;

/// <summary>
/// Authorization policy names used by the endpoints. Endpoints only name a policy; what the policy requires
/// is defined once in <see cref="AuthenticationExtensions.AddCrmAuthentication"/> (roles now, permissions in CRM-7).
/// </summary>
public static class CrmPolicies
{
    /// <summary>Create, edit, deactivate and list staff users (/api/users).</summary>
    public const string ManageUsers = "ManageUsers";
}
```

**Create file: `server/src/Crm.Api/Auth/HttpCurrentUser.cs`**

```csharp
using System.Security.Claims;
using Crm.Application.Common.Security;

namespace Crm.Api.Auth;

/// <summary>The current request's user, read from the validated JWT claims.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(AuthClaimTypes.UserId), out var id) ? id : null;

    // RoleClaimType is "role" (AddCrmAuthentication), so IsInRole reads our role claims.
    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
```

**File: `server/src/Crm.Api/Auth/AuthenticationExtensions.cs`** — final content (changes: three usings, `bearer.Events`, `ICurrentUser` registration, the policy builder replacing `services.AddAuthorization()`, and `RejectInactiveUserAsync`):

```csharp
using System.Security.Claims;
using Crm.Application.Auth;
using Crm.Application.Common.Security;
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
                bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectInactiveUserAsync };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Endpoints use policy names only. CRM-7 replaces RequireRole with a permission requirement here.
        services.AddAuthorizationBuilder()
            .AddPolicy(CrmPolicies.ManageUsers, policy => policy.RequireRole(Roles.SuperAdmin, Roles.Admin));
        return services;
    }

    /// <summary>
    /// A valid signature is not enough: the user must still exist and be active. A deactivated user's token
    /// stops working on the next request (401), not only when it expires.
    /// </summary>
    private static async Task RejectInactiveUserAsync(TokenValidatedContext context)
    {
        var checker = context.HttpContext.RequestServices.GetRequiredService<IActiveUserChecker>();
        var isActive = Guid.TryParse(context.Principal?.FindFirstValue(AuthClaimTypes.UserId), out var userId)
                       && await checker.IsActiveAsync(userId, context.HttpContext.RequestAborted);
        if (!isActive)
        {
            context.Fail("The user is deactivated or no longer exists.");
        }
    }

    /// <summary>A token without <c>exp</c> is rejected; <c>nbf</c> is optional.</summary>
    private static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, TimeSpan clockSkew, DateTime utcNow) =>
        expires is not null
        && expires.Value.ToUniversalTime() > utcNow - clockSkew
        && (notBefore is null || notBefore.Value.ToUniversalTime() <= utcNow + clockSkew);
}
```

`AddAuthorizationBuilder()` also registers the authorization services (it replaces `AddAuthorization()`); still **no fallback policy** (unknown routes must stay 404).

**Create file: `server/src/Crm.Api/Endpoints/UsersEndpoints.cs`**

```csharp
using Crm.Api.Auth;
using Crm.Application.Users;

namespace Crm.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        // Every endpoint: 401 without a valid token, 403 for users outside the ManageUsers policy.
        var group = app.MapGroup("/api/users").RequireAuthorization(CrmPolicies.ManageUsers);

        group.MapGet("", async ([AsParameters] ListUsersQuery query, IUserService users, CancellationToken cancellationToken) =>
                Results.Ok(await users.ListAsync(query, cancellationToken)))
            .WithName("ListUsers");

        group.MapGet("/{id:guid}", async (Guid id, IUserService users, CancellationToken cancellationToken) =>
                Results.Ok(await users.GetAsync(id, cancellationToken)))
            .WithName("GetUser");

        group.MapPost("", async (CreateUserRequest request, IUserService users, CancellationToken cancellationToken) =>
            {
                var user = await users.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/users/{user.Id}", user);
            })
            .WithName("CreateUser");

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, IUserService users, CancellationToken cancellationToken) =>
                Results.Ok(await users.UpdateAsync(id, request, cancellationToken)))
            .WithName("UpdateUser");

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IUserService users, CancellationToken cancellationToken) =>
            {
                await users.DeactivateAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeactivateUser");

        group.MapPost("/{id:guid}/reactivate", async (Guid id, IUserService users, CancellationToken cancellationToken) =>
            {
                await users.ReactivateAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("ReactivateUser");

        return app;
    }
}
```

**File: `server/src/Crm.Api/Program.cs`** — after line 32 (`app.MapAuthEndpoints();`) add `app.MapUsersEndpoints();`.

### 6 — Migration

From `server/` (same command style as CRM-2's `InitialIdentity`):

```bash
dotnet build
dotnet ef migrations add AddUserIsActive --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations
```

Expected: ends with `Done. To undo this action, use 'ef migrations remove'`; creates `<timestamp>_AddUserIsActive.cs` + `.Designer.cs` and updates `CrmDbContextModelSnapshot.cs` (adds `b.Property<bool>("IsActive").HasColumnType("bit").HasDefaultValue(true);`). `Up` must be exactly one `AddColumn<bool>(name: "IsActive", table: "AspNetUsers", type: "bit", nullable: false, defaultValue: true)`; `Down` drops the column. If `Up` says `defaultValue: false`, the `HasDefaultValue(true)` line is missing — run `dotnet ef migrations remove …` and fix the model first. **Do not** hand-edit migration files.

Run `dotnet test` → **Green: 141 passed** (62 unit + 79 integration: 42 existing + 14 `UserManagementTests` + 15 `UsersAuthorizationTests` + 8 `UserListTests`). The guard `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous` stays green (the group's `RequireAuthorization` covers all six routes).

Optional proof for the token rule: delete the `bearer.Events = …` line, run `dotnet test --filter DeactivatedUser_ExistingToken_StopsWorking` → fails with `Expected: Unauthorized, Actual: OK`; restore the line.

### 7 — How later stories build on this (write nothing here; for later planners)

- **CRM-7 (roles & permissions):** change only the policy definitions in `AddCrmAuthentication` (e.g. `CrmPolicies.ManageUsers` → a permission requirement); add new policy names to `CrmPolicies`. Endpoints and `UsersAuthorizationTests` keep working unchanged (Agent/Supervisor → 403, Admin/SuperAdmin → 200 must stay true unless CRM-7's matrix says otherwise). If permissions go into the token, add them in `JwtAccessTokenGenerator`; the `OnTokenValidated` check is the place to refresh/verify them per request if needed. `ICurrentUser.IsInRole` is available to services for rules finer than a policy.
- **Every later paged list** (customers, tickets, …): reuse `PagedResult<T>` + `PagingDefaults`, a `List<Feature>Query` record bound with `[AsParameters]`, a validator with the same page/pageSize rules, and `EF.Functions.Like` with escaping for `search`.
- **Services that record "who did it"** (tickets, comments, audit): inject `ICurrentUser` (Application) — never `HttpContext`.
- **Integration tests needing a role:** `await factory.CreateClientWithRoleAsync("Agent")` or `factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, roles)`.
- **Assigning tickets to users / showing agents:** only `IsActive` users should be offered; reuse `IUserService.ListAsync` or add a lightweight lookup endpoint in that story.
- **Performance:** the per-request `IActiveUserChecker` query is one primary-key lookup; if it ever matters, cache it briefly (e.g. `IMemoryCache`, invalidated by `UserService.SetActiveAsync`).

---

## Frontend Tasks

All commands run from `client/`. **No new npm packages.** Follow `vercel-react-best-practices` (direct imports, no barrel files).

### 1 — shadcn components

```bash
npx shadcn@4.21.2 add table dialog checkbox badge alert-dialog -y
```

Expected: "Created 5 files": `src/components/ui/table.tsx`, `checkbox.tsx`, `badge.tsx`, `dialog.tsx`, `alert-dialog.tsx`; "Skipped 1 file … button.tsx" (identical). `package.json` / `package-lock.json` unchanged. **Never hand-edit** `components/ui/`.

### 2 — Tests first (Red)

**File: `client/src/test/setup.ts`** — after the `matchMedia` block (ends line 21) add:

```ts
// jsdom has no ResizeObserver. Radix Checkbox (inside a <form>) measures itself with it.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
Object.defineProperty(window, 'ResizeObserver', { writable: true, configurable: true, value: ResizeObserverStub })

```

**Create file: `client/src/api/users.test.ts`**

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createUser, deactivateUser, listUsers, reactivateUser, updateUser } from './users'

function fakeFetch(status = 200, body: unknown = {}) {
  const fetchMock = vi.fn().mockResolvedValue(
    status === 204
      ? new Response(null, { status })
      : new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function sent(fetchMock: ReturnType<typeof vi.fn>) {
  const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
  return { path, method: init.method, body: init.body === undefined ? undefined : JSON.parse(String(init.body)) }
}

describe('users API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists users without a query string by default', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 1, pageSize: 20, totalCount: 0 })

    await listUsers({})

    expect(sent(fetchMock)).toMatchObject({ path: '/api/users', method: 'GET' })
  })

  it('sends search, page and pageSize in the query string', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 2, pageSize: 10, totalCount: 0 })

    await listUsers({ search: 'sara & co', page: 2, pageSize: 10 })

    expect(sent(fetchMock).path).toBe('/api/users?search=sara+%26+co&page=2&pageSize=10')
  })

  it('creates a user with POST /api/users', async () => {
    const fetchMock = fakeFetch(201, { id: 'u1' })
    const request = { email: 'a@crm.local', fullName: 'A', password: 'Agent#Pass1', roles: ['Agent' as const] }

    await createUser(request)

    expect(sent(fetchMock)).toEqual({ path: '/api/users', method: 'POST', body: request })
  })

  it('updates a user with PUT /api/users/{id}', async () => {
    const fetchMock = fakeFetch(200, { id: 'u1' })
    const request = { email: 'a@crm.local', fullName: 'A', roles: ['Admin' as const] }

    await updateUser('u1', request)

    expect(sent(fetchMock)).toEqual({ path: '/api/users/u1', method: 'PUT', body: request })
  })

  it('deactivates and reactivates with POST and no body', async () => {
    const deactivate = fakeFetch(204)
    await deactivateUser('u1')
    expect(sent(deactivate)).toEqual({ path: '/api/users/u1/deactivate', method: 'POST', body: undefined })

    const reactivate = fakeFetch(204)
    await reactivateUser('u1')
    expect(sent(reactivate)).toEqual({ path: '/api/users/u1/reactivate', method: 'POST', body: undefined })
  })
})
```

**Create file: `client/src/pages/users/UsersPage.test.tsx`** — AC 1, 2, 3, 5 from the user's side (API module mocked, real React Query + real dialogs + real toasts):

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/api/errors'
import {
  createUser,
  deactivateUser,
  listUsers,
  reactivateUser,
  updateUser,
  type PagedResult,
  type User,
} from '@/api/users'
import { createQueryClient } from '@/app/query-client'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { UsersPage } from './UsersPage'

vi.mock('@/api/users', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/users')>()),
  listUsers: vi.fn(),
  createUser: vi.fn(),
  updateUser: vi.fn(),
  deactivateUser: vi.fn(),
  reactivateUser: vi.fn(),
}))

const admin: User = { id: '1', email: 'admin@crm.local', fullName: 'System Administrator', roles: ['SuperAdmin'], isActive: true }
const sara: User = { id: '2', email: 'sara@crm.local', fullName: 'Sara Agent', roles: ['Agent', 'Supervisor'], isActive: true }
const omar: User = { id: '3', email: 'omar@crm.local', fullName: 'Omar Former', roles: ['Agent'], isActive: false }

function pageOf(items: User[], totalCount = items.length, page = 1): PagedResult<User> {
  return { items, page, pageSize: 20, totalCount }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <UsersPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function rowOf(name: string) {
  return screen.getByRole('row', { name: new RegExp(name) })
}

function fillUserForm(dialog: HTMLElement, values: { fullName?: string; email?: string; password?: string }) {
  if (values.fullName !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Full name'), { target: { value: values.fullName } })
  if (values.email !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Email'), { target: { value: values.email } })
  if (values.password !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Password'), { target: { value: values.password } })
}

describe('UsersPage', () => {
  beforeEach(() => {
    vi.mocked(listUsers).mockReset().mockResolvedValue(pageOf([admin, sara, omar]))
    vi.mocked(createUser).mockReset()
    vi.mocked(updateUser).mockReset()
    vi.mocked(deactivateUser).mockReset().mockResolvedValue(undefined)
    vi.mocked(reactivateUser).mockReset().mockResolvedValue(undefined)
  })

  it('lists users with email, roles and status', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Users' })).toBeInTheDocument()
    const row = await screen.findByRole('row', { name: /Sara Agent/ })
    expect(within(row).getByText('sara@crm.local')).toBeInTheDocument()
    expect(within(row).getByText('Support agent, Team supervisor')).toBeInTheDocument()
    expect(within(row).getByText('Active')).toBeInTheDocument()
    expect(within(rowOf('Omar Former')).getByText('Inactive')).toBeInTheDocument()
    expect(listUsers).toHaveBeenCalledWith({ search: undefined, page: 1, pageSize: 20 }, expect.anything())
  })

  it('searches by name or email and starts again at page 1', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })
    vi.mocked(listUsers).mockResolvedValue(pageOf([sara]))

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search by name or email' }), { target: { value: ' sara ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() =>
      expect(listUsers).toHaveBeenLastCalledWith({ search: 'sara', page: 1, pageSize: 20 }, expect.anything()),
    )
    await waitFor(() => expect(screen.queryByRole('row', { name: /Omar Former/ })).not.toBeInTheDocument())
  })

  it('shows "No users found." when nothing matches', async () => {
    vi.mocked(listUsers).mockResolvedValue(pageOf([]))
    renderPage()

    expect(await screen.findByText('No users found.')).toBeInTheDocument()
  })

  it('pages through the results', async () => {
    vi.mocked(listUsers).mockResolvedValue(pageOf([admin, sara], 45))
    renderPage()

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
    expect(listUsers).toHaveBeenLastCalledWith({ search: undefined, page: 2, pageSize: 20 }, expect.anything())
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('creates a user and reloads the list', async () => {
    vi.mocked(createUser).mockResolvedValue({ id: '4', email: 'lina@crm.local', fullName: 'Lina New', roles: ['Agent'], isActive: true })
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    const dialog = await screen.findByRole('dialog', { name: 'New user' })
    fillUserForm(dialog, { fullName: 'Lina New', email: 'lina@crm.local', password: 'Agent#Pass1' })
    fireEvent.click(within(dialog).getByRole('checkbox', { name: 'Support agent' }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(createUser).toHaveBeenCalledWith({
      fullName: 'Lina New',
      email: 'lina@crm.local',
      password: 'Agent#Pass1',
      roles: ['Agent'],
    })
    expect(await screen.findByText('User Lina New was created.')).toBeInTheDocument()
    expect(listUsers).toHaveBeenCalledTimes(2)
  })

  it('checks the form before calling the API', async () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    const dialog = await screen.findByRole('dialog', { name: 'New user' })
    fillUserForm(dialog, { email: 'not-an-email', password: 'weak' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Enter the full name.')).toBeInTheDocument()
    expect(within(dialog).getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(
      within(dialog).getByText('Use at least 8 characters with an upper-case letter, a lower-case letter, a digit and a symbol.'),
    ).toBeInTheDocument()
    expect(within(dialog).getByText('Select at least one role.')).toBeInTheDocument()
    expect(createUser).not.toHaveBeenCalled()
  })

  it('shows the server message next to the email when it is already used', async () => {
    vi.mocked(createUser).mockRejectedValue(
      new ApiError('POST /api/users failed with status 400', 400, {
        status: 400,
        errors: { email: ['This email is already used by another user.'] },
      }),
    )
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    const dialog = await screen.findByRole('dialog', { name: 'New user' })
    fillUserForm(dialog, { fullName: 'Copy', email: 'admin@crm.local', password: 'Agent#Pass1' })
    fireEvent.click(within(dialog).getByRole('checkbox', { name: 'Support agent' }))

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('This email is already used by another user.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
  })

  it('edits a user: the dialog starts with the current values and has no password field', async () => {
    vi.mocked(updateUser).mockResolvedValue({ ...sara, fullName: 'Sara Lead' })
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.click(within(rowOf('Sara Agent')).getByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit user' })
    expect(within(dialog).getByLabelText('Email')).toHaveValue('sara@crm.local')
    expect(within(dialog).queryByLabelText('Password')).not.toBeInTheDocument()
    expect(within(dialog).getByRole('checkbox', { name: 'Team supervisor' })).toBeChecked()
    fillUserForm(dialog, { fullName: 'Sara Lead' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(updateUser).toHaveBeenCalledWith('2', {
        fullName: 'Sara Lead',
        email: 'sara@crm.local',
        roles: ['Agent', 'Supervisor'],
      }),
    )
    expect(await screen.findByText('User Sara Lead was saved.')).toBeInTheDocument()
  })

  it('deactivates a user after confirmation', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.click(within(rowOf('Sara Agent')).getByRole('button', { name: 'Deactivate' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Deactivate Sara Agent?' })
    expect(deactivateUser).not.toHaveBeenCalled()
    fireEvent.click(within(confirm).getByRole('button', { name: 'Deactivate' }))

    await waitFor(() => expect(deactivateUser).toHaveBeenCalledWith('2'))
    expect(await screen.findByText('Sara Agent was deactivated.')).toBeInTheDocument()
  })

  it('reactivates an inactive user', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Omar Former/ })

    fireEvent.click(within(rowOf('Omar Former')).getByRole('button', { name: 'Reactivate' }))

    await waitFor(() => expect(reactivateUser).toHaveBeenCalledWith('3'))
  })
})
```

**File: `client/src/App.layout.test.tsx`**

- After line 7 (`NAVIGATION_LABELS`) add:

```ts
/** Areas whose story is not built yet (each later story removes its label from this list). */
const COMING_SOON_LABELS = ['Tickets', 'Customers', 'Knowledge base', 'Reports']
```

- Before the test at line 119 add:

```tsx
  it('opens the users page from the sidebar', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })

    fireEvent.click(within(navigation).getByRole('link', { name: 'Users' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Users' })).toBeInTheDocument()
    expect(await screen.findByRole('row', { name: /System Administrator/ })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/users')
    expect(within(navigation).getByRole('link', { name: 'Users' })).toHaveAttribute('aria-current', 'page')
  })

```

- Line 125: `for (const label of NAVIGATION_LABELS.slice(1)) {` becomes `for (const label of COMING_SOON_LABELS) {`.

**File: `client/src/test/fake-api.ts`** — after the `/api/auth/me` block (ends line 39) add:

```ts
    if (path === '/api/users' || path.startsWith('/api/users?')) {
      return json(200, { items: [{ ...me, isActive: true }], page: 1, pageSize: 20, totalCount: 1 })
    }
```

Run `npm test` → **Red**: `users.test.ts` and `UsersPage.test.tsx` fail to import (`./users`, `./UsersPage` do not exist); `opens the users page from the sidebar` fails (Users still shows "coming soon").

### 3 — API module

**File: `client/src/api/client.ts`** — after `apiPost` (ends line 85) add:

```ts

export function apiPut<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  return request<T>('PUT', path, { body, signal })
}
```

**Create file: `client/src/api/users.ts`**

```ts
import { apiGet, apiPost, apiPut } from './client'

/** Role names seeded by the API (server: Crm.Application.Auth.Roles). Labels: t(`users.roleNames.${role}`). */
export const roleNames = ['SuperAdmin', 'Admin', 'Supervisor', 'Agent'] as const
export type RoleName = (typeof roleNames)[number]

export interface User {
  id: string
  email: string
  fullName: string
  roles: RoleName[]
  isActive: boolean
}

/** One page of a list (server: PagedResult<T>). */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ListUsersParams {
  search?: string
  page?: number
  pageSize?: number
}

export interface CreateUserRequest {
  email: string
  fullName: string
  password: string
  roles: RoleName[]
}

export interface UpdateUserRequest {
  email: string
  fullName: string
  roles: RoleName[]
}

export function listUsers({ search, page, pageSize }: ListUsersParams, signal?: AbortSignal): Promise<PagedResult<User>> {
  const query = new URLSearchParams()
  if (search) query.set('search', search)
  if (page !== undefined) query.set('page', String(page))
  if (pageSize !== undefined) query.set('pageSize', String(pageSize))
  const queryString = query.toString()
  return apiGet<PagedResult<User>>(queryString ? `/api/users?${queryString}` : '/api/users', signal)
}

export function createUser(request: CreateUserRequest): Promise<User> {
  return apiPost<User>('/api/users', request)
}

export function updateUser(id: string, request: UpdateUserRequest): Promise<User> {
  return apiPut<User>(`/api/users/${encodeURIComponent(id)}`, request)
}

export function deactivateUser(id: string): Promise<void> {
  return apiPost<void>(`/api/users/${encodeURIComponent(id)}/deactivate`, undefined)
}

export function reactivateUser(id: string): Promise<void> {
  return apiPost<void>(`/api/users/${encodeURIComponent(id)}/reactivate`, undefined)
}
```

A `body` of `undefined` sends no body and no `Content-Type` (`client.ts` line 43).

### 4 — Translations

**File: `client/src/i18n/en.json`** — after the `toast` block (lines 50–53; add a comma after its `}`) add:

```json
  "users": {
    "description": "Create, edit and deactivate staff users.",
    "add": "Add user",
    "searchLabel": "Search by name or email",
    "search": "Search",
    "columns": {
      "name": "Name",
      "email": "Email",
      "roles": "Roles",
      "status": "Status",
      "actions": "Actions"
    },
    "active": "Active",
    "inactive": "Inactive",
    "edit": "Edit",
    "deactivate": "Deactivate",
    "reactivate": "Reactivate",
    "empty": "No users found.",
    "loading": "Loading users…",
    "pageInfo": "Page {{page}} of {{pages}}",
    "previous": "Previous",
    "next": "Next",
    "createTitle": "New user",
    "createDescription": "The user signs in with this email and password.",
    "editTitle": "Edit user",
    "editDescription": "Change the name, email or roles.",
    "fullName": "Full name",
    "email": "Email",
    "password": "Password",
    "roles": "Roles",
    "save": "Save",
    "saving": "Saving…",
    "cancel": "Cancel",
    "fullNameRequired": "Enter the full name.",
    "emailRequired": "Enter the email.",
    "emailInvalid": "Enter a valid email address.",
    "passwordWeak": "Use at least 8 characters with an upper-case letter, a lower-case letter, a digit and a symbol.",
    "rolesRequired": "Select at least one role.",
    "created": "User {{name}} was created.",
    "updated": "User {{name}} was saved.",
    "deactivateTitle": "Deactivate {{name}}?",
    "deactivateDescription": "They will no longer be able to sign in. You can reactivate them later.",
    "deactivated": "{{name}} was deactivated.",
    "reactivated": "{{name}} was reactivated.",
    "roleNames": {
      "SuperAdmin": "System administrator",
      "Admin": "Administrator",
      "Supervisor": "Team supervisor",
      "Agent": "Support agent"
    }
  }
```

**File: `client/src/i18n/ar.json`** — same place, same keys:

```json
  "users": {
    "description": "إنشاء مستخدمي فريق العمل وتعديلهم وإيقافهم.",
    "add": "إضافة مستخدم",
    "searchLabel": "البحث بالاسم أو البريد الإلكتروني",
    "search": "بحث",
    "columns": {
      "name": "الاسم",
      "email": "البريد الإلكتروني",
      "roles": "الأدوار",
      "status": "الحالة",
      "actions": "الإجراءات"
    },
    "active": "نشط",
    "inactive": "موقوف",
    "edit": "تعديل",
    "deactivate": "إيقاف",
    "reactivate": "إعادة تفعيل",
    "empty": "لا يوجد مستخدمون.",
    "loading": "جارٍ تحميل المستخدمين…",
    "pageInfo": "صفحة {{page}} من {{pages}}",
    "previous": "السابق",
    "next": "التالي",
    "createTitle": "مستخدم جديد",
    "createDescription": "يسجّل المستخدم الدخول بهذا البريد الإلكتروني وكلمة المرور.",
    "editTitle": "تعديل المستخدم",
    "editDescription": "غيّر الاسم أو البريد الإلكتروني أو الأدوار.",
    "fullName": "الاسم الكامل",
    "email": "البريد الإلكتروني",
    "password": "كلمة المرور",
    "roles": "الأدوار",
    "save": "حفظ",
    "saving": "جارٍ الحفظ…",
    "cancel": "إلغاء",
    "fullNameRequired": "أدخل الاسم الكامل.",
    "emailRequired": "أدخل البريد الإلكتروني.",
    "emailInvalid": "أدخل بريداً إلكترونياً صحيحاً.",
    "passwordWeak": "استخدم 8 أحرف على الأقل تتضمن حرفاً كبيراً وحرفاً صغيراً ورقماً ورمزاً.",
    "rolesRequired": "اختر دوراً واحداً على الأقل.",
    "created": "تم إنشاء المستخدم {{name}}.",
    "updated": "تم حفظ المستخدم {{name}}.",
    "deactivateTitle": "إيقاف {{name}}؟",
    "deactivateDescription": "لن يتمكن من تسجيل الدخول بعد الآن. يمكنك إعادة تفعيله لاحقاً.",
    "deactivated": "تم إيقاف {{name}}.",
    "reactivated": "تمت إعادة تفعيل {{name}}.",
    "roleNames": {
      "SuperAdmin": "مدير النظام",
      "Admin": "مسؤول",
      "Supervisor": "مشرف فريق",
      "Agent": "موظف دعم"
    }
  }
```

The English role labels are deliberately **not** the role ids ("Support agent", not "Agent"): `no-hardcoded-text.test.ts` fails when an English value longer than 3 characters appears quoted in code, and `'Agent'` / `'Supervisor'` are quoted in `client/src/api/users.ts`.

### 5 — Feature components and page

**Create file: `client/src/features/users/user-form-schema.ts`**

```ts
import type { TFunction } from 'i18next'
import { z } from 'zod'
import { roleNames } from '@/api/users'

/** Same policy as the API (8+ characters, upper-case, lower-case, digit, symbol); the server checks it again. */
export function isStrongPassword(password: string): boolean {
  return (
    password.length >= 8 &&
    /[A-Z]/.test(password) &&
    /[a-z]/.test(password) &&
    /\d/.test(password) &&
    /[^A-Za-z0-9]/.test(password)
  )
}

/**
 * Client-side checks of the user dialog (the server validates again).
 * "create" requires a strong password; "edit" has no password field (the value is ignored).
 */
export function createUserFormSchema(t: TFunction, mode: 'create' | 'edit') {
  return z.object({
    fullName: z.string().trim().min(1, t('users.fullNameRequired')).max(200),
    email: z.string().trim().min(1, t('users.emailRequired')).pipe(z.email(t('users.emailInvalid'))),
    password: mode === 'create' ? z.string().refine(isStrongPassword, t('users.passwordWeak')) : z.string(),
    roles: z.array(z.enum(roleNames)).min(1, t('users.rolesRequired')),
  })
}

export type UserFormValues = z.infer<ReturnType<typeof createUserFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const userFormFields = ['fullName', 'email', 'password', 'roles'] as const
```

**Create file: `client/src/features/users/useUsers.ts`**

```ts
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { listUsers, type ListUsersParams } from '@/api/users'

/** Prefix of every users query: mutations invalidate it so every page and search reloads. */
export const usersQueryKey = ['users'] as const

/** One page of GET /api/users. The previous page stays visible while the next one loads. */
export function useUsers(params: ListUsersParams) {
  return useQuery({
    queryKey: [...usersQueryKey, params],
    queryFn: ({ signal }) => listUsers(params, signal),
    placeholderData: keepPreviousData,
  })
}
```

**Create file: `client/src/features/users/UserFormDialog.tsx`**

```tsx
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { createUser, roleNames, updateUser, type User } from '@/api/users'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Field, FieldError, FieldGroup, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { createUserFormSchema, userFormFields, type UserFormValues } from './user-form-schema'
import { usersQueryKey } from './useUsers'

interface UserFormDialogProps {
  /** The user to edit; without it the dialog creates a new user. */
  user?: User
  onClose: () => void
}

/** Create / edit dialog. Mounted only while open (key per user), so the form always starts from fresh values. */
export function UserFormDialog({ user, onClose }: UserFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const mode = user ? 'edit' : 'create'
  const schema = useMemo(() => createUserFormSchema(t, mode), [t, mode])
  const form = useForm<UserFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      fullName: user?.fullName ?? '',
      email: user?.email ?? '',
      password: '',
      roles: user?.roles ?? [],
    },
  })

  const save = useMutation({
    mutationFn: ({ password, ...values }: UserFormValues) =>
      user ? updateUser(user.id, values) : createUser({ ...values, password }),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: usersQueryKey })
      toast.success(t(user ? 'users.updated' : 'users.created', { name: saved.fullName }))
      onClose()
    },
  })

  async function onSubmit(values: UserFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400: show the server's field messages (already in the UI language) next to the fields.
      // Every failure also shows a toast (ApiErrorToaster).
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of userFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t(user ? 'users.editTitle' : 'users.createTitle')}</DialogTitle>
          <DialogDescription>{t(user ? 'users.editDescription' : 'users.createDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="fullName"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="user-full-name">{t('users.fullName')}</FieldLabel>
                  <Input {...field} id="user-full-name" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="email"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="user-email">{t('users.email')}</FieldLabel>
                  <Input {...field} id="user-email" type="email" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            {user ? null : (
              <Controller
                name="password"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="user-password">{t('users.password')}</FieldLabel>
                    <Input
                      {...field}
                      id="user-password"
                      type="password"
                      autoComplete="new-password"
                      aria-invalid={fieldState.invalid}
                    />
                    {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                  </Field>
                )}
              />
            )}
            <Controller
              name="roles"
              control={form.control}
              render={({ field, fieldState }) => (
                <FieldSet data-invalid={fieldState.invalid}>
                  <FieldLegend variant="label">{t('users.roles')}</FieldLegend>
                  {roleNames.map((role) => (
                    <Field key={role} orientation="horizontal">
                      <Checkbox
                        id={`user-role-${role}`}
                        checked={field.value.includes(role)}
                        aria-invalid={fieldState.invalid}
                        onCheckedChange={(checked) =>
                          field.onChange(
                            checked === true ? [...field.value, role] : field.value.filter((value) => value !== role),
                          )
                        }
                      />
                      <FieldLabel htmlFor={`user-role-${role}`}>{t(`users.roleNames.${role}`)}</FieldLabel>
                    </Field>
                  ))}
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </FieldSet>
              )}
            />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('users.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('users.saving') : t('users.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}
```

`showCloseButton={false}`: the generated close button contains the English sr-only text "Close" (`components/ui/dialog.tsx` line 77); "Cancel" and Escape close the dialog instead.

**Create file: `client/src/features/users/UserStatusAction.tsx`**

```tsx
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { deactivateUser, reactivateUser, type User } from '@/api/users'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { usersQueryKey } from './useUsers'

/** Deactivate button (asks for confirmation first) for an active user, reactivate button for an inactive one. */
export function UserStatusAction({ user }: { user: User }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const change = useMutation({
    mutationFn: () => (user.isActive ? deactivateUser(user.id) : reactivateUser(user.id)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: usersQueryKey })
      toast.success(t(user.isActive ? 'users.deactivated' : 'users.reactivated', { name: user.fullName }))
    },
  })

  if (!user.isActive) {
    return (
      <Button variant="outline" size="sm" disabled={change.isPending} onClick={() => change.mutate()}>
        {t('users.reactivate')}
      </Button>
    )
  }

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button variant="outline" size="sm" disabled={change.isPending}>
          {t('users.deactivate')}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t('users.deactivateTitle', { name: user.fullName })}</AlertDialogTitle>
          <AlertDialogDescription>{t('users.deactivateDescription')}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{t('users.cancel')}</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={() => change.mutate()}>
            {t('users.deactivate')}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
```

**Do not** quote UI strings in comments (e.g. `"Deactivate"`) — `no-hardcoded-text.test.ts` reads raw source and fails (seen while planning).

**Create file: `client/src/features/users/UsersTable.tsx`**

```tsx
import { useTranslation } from 'react-i18next'
import type { User } from '@/api/users'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { UserStatusAction } from './UserStatusAction'

interface UsersTableProps {
  users: User[]
  onEdit: (user: User) => void
}

export function UsersTable({ users, onEdit }: UsersTableProps) {
  const { t, i18n } = useTranslation()
  // List punctuation of the UI language (comma + space in English, Arabic conjunction in Arabic).
  const roleList = new Intl.ListFormat(i18n.language, { type: 'unit' })

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('users.columns.name')}</TableHead>
          <TableHead>{t('users.columns.email')}</TableHead>
          <TableHead>{t('users.columns.roles')}</TableHead>
          <TableHead>{t('users.columns.status')}</TableHead>
          <TableHead className="text-end">{t('users.columns.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {users.map((user) => (
          <TableRow key={user.id}>
            <TableCell className="font-medium">{user.fullName}</TableCell>
            <TableCell>{user.email}</TableCell>
            <TableCell>{roleList.format(user.roles.map((role) => t(`users.roleNames.${role}`)))}</TableCell>
            <TableCell>
              <Badge variant={user.isActive ? 'secondary' : 'outline'}>
                {t(user.isActive ? 'users.active' : 'users.inactive')}
              </Badge>
            </TableCell>
            <TableCell>
              <div className="flex justify-end gap-2">
                <Button variant="outline" size="sm" onClick={() => onEdit(user)}>
                  {t('users.edit')}
                </Button>
                <UserStatusAction user={user} />
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
```

(`text-end` / `justify-end` are logical: they flip in Arabic. `theme.test.ts` rejects `text-right`, `ml-*` etc.)

**Create file: `client/src/pages/users/UsersPage.tsx`**

```tsx
import { PlusIcon, SearchIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { User } from '@/api/users'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { UserFormDialog } from '@/features/users/UserFormDialog'
import { UsersTable } from '@/features/users/UsersTable'
import { useUsers } from '@/features/users/useUsers'

const PAGE_SIZE = 20

type DialogState = { mode: 'create' } | { mode: 'edit'; user: User } | null

/** Users admin page: search, paged table, create / edit dialog, deactivate / reactivate. */
export function UsersPage() {
  const { t } = useTranslation()
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [dialog, setDialog] = useState<DialogState>(null)
  const users = useUsers({ search: search || undefined, page, pageSize: PAGE_SIZE })

  const totalPages = users.data ? Math.max(1, Math.ceil(users.data.totalCount / users.data.pageSize)) : 1

  function onSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchText.trim())
    setPage(1)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold">{t('nav.users')}</h1>
          <p className="text-muted-foreground">{t('users.description')}</p>
        </div>
        <Button onClick={() => setDialog({ mode: 'create' })}>
          <PlusIcon aria-hidden="true" />
          {t('users.add')}
        </Button>
      </div>

      <form role="search" className="flex max-w-md gap-2" onSubmit={onSearch}>
        <Input
          type="search"
          aria-label={t('users.searchLabel')}
          placeholder={t('users.searchLabel')}
          value={searchText}
          onChange={(event) => setSearchText(event.target.value)}
        />
        <Button type="submit" variant="outline">
          <SearchIcon aria-hidden="true" />
          {t('users.search')}
        </Button>
      </form>

      {users.isPending ? (
        <p className="text-muted-foreground">{t('users.loading')}</p>
      ) : users.data && users.data.items.length > 0 ? (
        <UsersTable users={users.data.items} onEdit={(user) => setDialog({ mode: 'edit', user })} />
      ) : (
        <p className="text-muted-foreground">{t('users.empty')}</p>
      )}

      <div className="flex items-center justify-end gap-2">
        <span className="text-sm text-muted-foreground">{t('users.pageInfo', { page, pages: totalPages })}</span>
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          {t('users.previous')}
        </Button>
        <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
          {t('users.next')}
        </Button>
      </div>

      {dialog ? (
        <UserFormDialog
          key={dialog.mode === 'edit' ? dialog.user.id : 'new'}
          user={dialog.mode === 'edit' ? dialog.user : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}
```

Search runs on submit (button or Enter), not on every keystroke — no debounce timers, one request per search.

**File: `client/src/app/AppRoutes.tsx`** — after line 5 add `import { UsersPage } from '@/pages/users/UsersPage'`; delete line 20 (`users` coming-soon route) and add, right after the dashboard route (line 14):

```tsx
          <Route path="users" element={<UsersPage />} />
```

`ComingSoonPage` stays (still used by tickets, customers, knowledge base, reports).

Run `npm test` → **Green: 364 passed in 18 files** (213 existing + 135 new `translations.test.ts` rows for 45 new keys × 3 checks + 5 `users.test.ts` + 10 `UsersPage.test.tsx` + 1 `App.layout.test.tsx`). Then `npm run build` and `npm run lint`.

---

## Edge Cases & Failure Modes

- **Duplicate email, any case** (`ADMIN@CRM.LOCAL`) → `FindByEmailAsync` uses `NormalizedEmail` → 400 `errors.email` (`UserService.CreateAsync` / `UpdateAsync`). Covered by `CreateUser_WithExistingEmail_Returns400WithEmailError` (×2) and `UpdateUser_WithEmailOfAnotherUser_Returns400`. Updating a user with **their own** email is allowed (`owner.Id != user.Id`).
- **Two admins create the same email at the same moment** → the second `CreateAsync` fails Identity's `DuplicateEmail` / unique `UserNameIndex` → `ThrowIfFailed` → 400 on `email` (Identity's English text) or, if the race passes Identity's check, a DB unique-index error → 500. Accepted (rare, admin-only).
- **Password accepted by the validator but rejected by Identity** → impossible today (same rules); if Identity's options change, `ThrowIfFailed` maps `Password*` codes to `errors.password` (English Identity text).
- **Email with characters Identity forbids in user names** (`UserName = email`; default allowed set is letters, digits, `-._@+`) → Identity `InvalidUserName` → 400 `errors.email` with English text.
- **Deactivated user logs in** → `AuthService.LoginAsync` line 19 → 401 "Invalid email or password." (no hint that the account exists). Covered by `DeactivatedUser_TryingToLogIn_Gets401`.
- **Deactivated user still holds a token** → `RejectInactiveUserAsync` → 401 on the next request; the client clears the session (CRM-2 rule) and shows the login page. Covered by `DeactivatedUser_ExistingToken_StopsWorking`. Also covers a token whose user was deleted from the DB.
- **Role removed from a signed-in user** → their token keeps the old `role` claims until it expires (≤ 60 min) or they sign in again. Documented decision; CRM-7 may tighten.
- **Admin deactivates themselves** → 409 "You cannot deactivate your own account." (`UserService.DeactivateAsync`). Covered by `Deactivate_YourOwnAccount_Returns409`. Removing their own Admin role via edit is **allowed** (they lose access at next login) — not blocked in this story.
- **Admin (not SuperAdmin) assigns SuperAdmin, or edits/deactivates/reactivates a SuperAdmin** → 403 `UserText.SuperAdminOnly` (`EnsureMayManage`). Covered by `Admin_CannotCreateASuperAdmin_Returns403`, `Admin_CannotDeactivateTheSuperAdmin_Returns403`. The dialog still shows the SuperAdmin checkbox to Admins (server refuses; CRM-7 hides it).
- **Last SuperAdmin** → only SuperAdmins can deactivate SuperAdmins and nobody can deactivate themselves, so at least one active SuperAdmin always remains.
- **Deactivate an inactive user / reactivate an active user** → 204, no change (idempotent `SetActiveAsync`).
- **Unknown id** → 404 "The user was not found."; a non-GUID id (`/api/users/abc`) → route constraint `{id:guid}` → 404.
- **Non-admin** → authorization middleware → empty 403 → `UseStatusCodePages` → ProblemDetails, title `ErrorText.Forbidden` in the request language. No token → 401. Covered by `UsersAuthorizationTests` for all six endpoints.
- **`page` / `pageSize` out of range** → 400 with `page` / `pageSize` keys (`ListUsersQueryValidator`). Non-numeric (`page=abc`) → minimal-API binding → `BadHttpRequestException` → 400 "The request is malformed." Page beyond the last → 200 with empty `items` and the real `totalCount`.
- **Search with `%`, `_` or `\`** → escaped in `EscapeLike` → matched literally. Covered by `List_SearchTreatsWildcardsAsText`. Leading/trailing spaces are trimmed; empty search = no filter.
- **Search case-insensitivity on other collations** → relies on SQL Server's default case-insensitive collation (LocalDB default `SQL_Latin1_General_CP1_CI_AS`). A case-sensitive production collation would make search case-sensitive.
- **Per-request DB lookup** (`IActiveUserChecker`) → one `AnyAsync` on the primary key for every authenticated call; anonymous calls (`/api/health`, login) do not hit it.
- **Migration on an existing database** → `AddColumn … defaultValue: true` makes existing users (the seeded SuperAdmin) active. Verified on LocalDB.
- **Client: 400 on save** → the server's (already translated) message under the field **and** the generic CRM-5 toast "The request is invalid. Check the entered data." (ApiErrorToaster toasts every non-401 failure). Accepted.
- **Client: 403 for an Admin creating a SuperAdmin** → toast "You do not have permission to do this."; the dialog stays open.
- **Client: list fails (network / 403 for a non-admin opening `/users`)** → toast + "No users found." (the menu item is still visible to non-admins until CRM-7).
- **Client: language switch while the table is shown** → labels and role names re-render; role lists use `Intl.ListFormat` of the UI language.
- **Client tests: `ResizeObserver` missing in jsdom** → stub in `client/src/test/setup.ts` (Radix Checkbox).

---

## Test Plan

1. **Unit (new)** — `server/tests/Crm.UnitTests/Users/UserRequestValidatorTests.cs`: `ValidCreateRequest_HasNoErrors`, `CreateRequest_WithBadEmail_ReportsEmail` ×2, `CreateRequest_WithoutFullName_ReportsFullName` ×2, `CreateRequest_WithWeakPassword_ReportsPassword` ×5, `CreateRequest_WithoutRoles_ReportsRoles`, `CreateRequest_WithUnknownRole_ReportsRoles`, `CreateRequest_InArabic_HasArabicMessages`, `UpdateRequest_ValidAndInvalid`, `ListQuery_PagingLimits` ×5 (AC 2 validation part, AC 5 paging limits).
2. **Guard (modified)** — `server/tests/Crm.UnitTests/Localization/LocalizedTextCatalogTests.cs`: `Catalog_FindsTheTextClasses` includes `UserText`; 12 new `TextProperty_HasEnglishAndArabicText` rows.
3. **Integration (new)** — `server/tests/Crm.Api.IntegrationTests/Users/UserManagementTests.cs`: `CreateUser_ThenTheNewUserCanLogIn` (AC 1), `CreateUser_WithExistingEmail_Returns400WithEmailError` ×2 (AC 2), `CreateUser_WithInvalidData_Returns400WithFieldErrors`, `GetUser_ReturnsTheUser_AndUnknownIdReturns404`, `UpdateUser_ChangesNameEmailAndRoles`, `UpdateUser_WithEmailOfAnotherUser_Returns400`, `UpdateUser_UnknownId_Returns404`, `DeactivatedUser_TryingToLogIn_Gets401` (AC 3), `DeactivatedUser_ExistingToken_StopsWorking`, `ReactivatedUser_CanLogInAgain`, `Deactivate_YourOwnAccount_Returns409`, `Admin_CannotCreateASuperAdmin_Returns403`, `Admin_CannotDeactivateTheSuperAdmin_Returns403`.
4. **Integration (new)** — `server/tests/Crm.Api.IntegrationTests/Users/UsersAuthorizationTests.cs`: `UsersApi_AsAgent_Returns403` ×6 (AC 4), `UsersApi_AsSupervisor_Returns403` (AC 4), `UsersApi_WithoutToken_Returns401` ×6, `UsersApi_AsAdminOrSuperAdmin_Returns200` ×2.
5. **Integration (new)** — `server/tests/Crm.Api.IntegrationTests/Users/UserListTests.cs`: `List_WithoutParameters_ReturnsFirstPageOf20WithTotalCount`, `List_SearchByName_ReturnsOnlyMatchingUsers`, `List_SearchByEmail_IsCaseInsensitive`, `List_SearchTreatsWildcardsAsText`, `List_Paginates_WithTotalCountOfAllMatches`, `List_WithInvalidPaging_Returns400` ×3 (AC 5).
6. **Test host (modified)** — `CrmApiFactory`: `TestUserPassword`, `CreateUserAsync`, `CreateClientWithRoleAsync`.
7. **Unchanged, must stay green (backend)** — all CRM-5/CRM-2/CRM-4 tests, especially `EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous`, `LoginTests`, `TokenExpiryTests`, `AcceptLanguageTests`, `LayerDependencyTests` (Application still free of ASP.NET Core / EF / Identity).
8. **Unit (frontend, new)** — `client/src/api/users.test.ts`: default list path, query string, POST create, PUT update, deactivate/reactivate without body.
9. **Component (frontend, new)** — `client/src/pages/users/UsersPage.test.tsx`: `lists users with email, roles and status` (AC 5), `searches by name or email and starts again at page 1` (AC 5), `shows "No users found." when nothing matches`, `pages through the results` (AC 5), `creates a user and reloads the list` (AC 1), `checks the form before calling the API`, `shows the server message next to the email when it is already used` (AC 2), `edits a user: …`, `deactivates a user after confirmation` (AC 3), `reactivates an inactive user`.
10. **App level (modified)** — `client/src/App.layout.test.tsx`: new `opens the users page from the sidebar`; "coming soon" loop over `COMING_SOON_LABELS`.
11. **Guards (unchanged files, more rows)** — `translations.test.ts` (45 new keys), `no-hardcoded-text.test.ts`, `theme.test.ts` (new components: no hex colors; app files: logical classes).
12. **Manual smoke** — Verification step 7.

---

## Migration / Rollback

- **Schema:** migration `AddUserIsActive` adds `AspNetUsers.IsActive bit NOT NULL DEFAULT 1`. Applied automatically by `dotnet run` in Development (`Database:StartupAction = Migrate`).
- **Rollback (local DB):** stop the API; from `server/`: `dotnet ef database update InitialIdentity --project src/Crm.Infrastructure --startup-project src/Crm.Api` (drops the column), then `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api` if the migration was not committed yet.
- **Half-applied state:** the single `AddColumn` runs in the migration transaction; either the column exists with all rows active or nothing changed.
- **Code rollback without DB rollback:** older code ignores the extra column (it has a default) — safe.

---

## Verification Steps

1. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings.
2. **Backend tests:** in `server/` run `dotnet test` — **141 passed** (62 unit, 79 integration), 0 failed.
3. **Frontend tests:** in `client/` run `npm test` — **364 passed** in 18 files.
4. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed (the "chunks larger than 500 kB" message is a warning).
5. **Frontend lint:** in `client/` run `npm run lint` — exit code 0, no warnings, no errors.
6. **Migration check:** `git status` shows exactly `server/src/Crm.Infrastructure/Persistence/Migrations/<timestamp>_AddUserIsActive.cs`, `<timestamp>_AddUserIsActive.Designer.cs` and the modified `CrmDbContextModelSnapshot.cs`; `Up` contains `defaultValue: true`.
7. **Manual smoke** (user-secrets from CRM-2 already set):
   - Terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http` (applies `AddUserIsActive`).
   - Terminal 2: `cd client && npm run dev`, open `http://localhost:5173`, sign in as `admin@crm.local`, click **Users** → table with "System Administrator", roles "System administrator", status "Active", "Page 1 of 1".
   - **Add user** → "Agent One", `agent1@crm.local`, `Agent#Pass1`, check "Support agent", **Save** → toast "User Agent One was created.", row appears. Add the same email again → "This email is already used by another user." under Email.
   - Type `agent` in the search box + Enter → only Agent One.
   - Open a private window, sign in as `agent1@crm.local` → dashboard. Open `/users` there → toast "You do not have permission to do this." (403).
   - Back as admin: **Deactivate** Agent One → confirm → status "Inactive". In the private window, click any menu item → back to the login page (token rejected). Sign in again as agent1 → "Invalid email or password."
   - **Reactivate** → agent1 can sign in again. **Edit** → change the name → toast "User … was saved.".
   - Click **العربية** → page, dialog, role names and statuses in Arabic, table right-to-left.
8. **Regression:** `git status` shows no changes under `.claude/`, `.mcp.json`, `CLAUDE.md`; no hand edits in existing `client/src/components/ui/` files (only the 5 new generated files); `client/src/api/client.ts` is still the only `fetch` caller (`git grep -n "fetch(" -- client/src ':!*.test.*' ':!client/src/test'`).

---

## Done Criteria

- [ ] Admin creates a user → that user can log in (`CreateUser_ThenTheNewUserCanLogIn`, UI: `creates a user and reloads the list`).
- [ ] Existing email (any case) → 400 with `errors.email` (`CreateUser_WithExistingEmail_Returns400WithEmailError`, UI: `shows the server message next to the email when it is already used`).
- [ ] Deactivated user login → 401 (`DeactivatedUser_TryingToLogIn_Gets401`); their existing token → 401 (`DeactivatedUser_ExistingToken_StopsWorking`); reactivation restores access.
- [ ] Non-admin (Agent, Supervisor) on any `/api/users` endpoint → 403 (`UsersAuthorizationTests`); policy `CrmPolicies.ManageUsers` = SuperAdmin + Admin, defined only in `AddCrmAuthentication`.
- [ ] `GET /api/users` supports `search` (name/email, case-insensitive, literal wildcards) and `page`/`pageSize` (default 1/20, max 100) returning `PagedResult<UserResponse>` (`UserListTests`, UI: search + paging tests).
- [ ] `ApplicationUser.IsActive` + migration `AddUserIsActive` (`defaultValue: true`) in `Crm.Infrastructure/Persistence/Migrations`.
- [ ] Self-deactivation → 409; Admin cannot assign/change SuperAdmin → 403.
- [ ] `/users` shows the users page (no longer "coming soon"); every new string in `en.json` and `ar.json`; server texts in `UserText`.
- [ ] Only the 5 shadcn components added with `shadcn@4.21.2`; no new npm or NuGet packages.
- [ ] `dotnet build`, `dotnet test` (141), `npm test` (364), `npm run build`, `npm run lint` all pass.
- [ ] Committed on `feature/crm-6-user-management` with message `CRM-6: user management`.
- [ ] `.squad/plans/security-admin/00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 07.**
