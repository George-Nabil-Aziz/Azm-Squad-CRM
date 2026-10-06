# Story 08 — Customer profiles (CRUD) (Story: CRM-8)

## Prerequisites

- Foundation feature completed and merged to `main` ([../foundation/00-overview.md](../foundation/00-overview.md)):
  - Story 02 [../foundation/02-story-global-error-handling.md](../foundation/02-story-global-error-handling.md) (CRM-5) — ProblemDetails, `ValidationException` / `NotFoundException`, `ValidateOrThrowAsync`, `CrmApiFactory`.
  - Story 03 [../foundation/03-story-authentication.md](../foundation/03-story-authentication.md) (CRM-2) — `CrmDbContext`, migrations, `TimeProvider` registration, `FakeTimeProvider` in `CrmApiFactory.Time`. Line 1841 is **binding**: "Later entities are added to `CrmDbContext` (with `IsDeleted` query filters per CLAUDE.md) and each change gets its own migration."
  - Story 04 [../foundation/04-story-app-layout.md](../foundation/04-story-app-layout.md) (CRM-3) — line 1288 is **binding** ("New feature page (CRM-8 customers …): create `client/src/pages/<area>/<Name>Page.tsx`, replace the area's `ComingSoonPage` line … update its expectations when an area gets a real page").
  - Story 05 [../foundation/05-story-i18n-rtl.md](../foundation/05-story-i18n-rtl.md) (CRM-4) — lines 1718 and 1721 are **binding** (UI strings in both JSON files under `customers.*`; server texts in a `CustomerText` class).
- security-admin feature completed and merged to `main` ([../security-admin/00-overview.md](../security-admin/00-overview.md)):
  - Story 06 [../security-admin/06-story-user-management.md](../security-admin/06-story-user-management.md) (CRM-6) — `PagedResult<T>` + `PagingDefaults`, `[AsParameters] List…Query`, `EF.Functions.Like` with escaping, the users page as the UI precedent. Line 1382 is **binding** ("Every later paged list (customers, tickets, …): reuse `PagedResult<T>` + `PagingDefaults`, a `List<Feature>Query` record bound with `[AsParameters]`, a validator with the same page/pageSize rules, and `EF.Functions.Like` with escaping for `search`").
  - Story 07 [../security-admin/07-story-roles-permissions.md](../security-admin/07-story-roles-permissions.md) (CRM-7) — the permission catalogue (lines 40–56: `customers.view` / `customers.manage`, **every** role has both) and its section 5 (lines 671–681) are **binding**: customers group `RequireAuthorization(Permissions.CustomersView)` plus `RequireAuthorization(Permissions.CustomersManage)` on the write endpoints; the client route stays inside the existing `RequirePermission` (replace the `ComingSoonPage` element only); actions behind `<Can>`; page tests mock `@/api/auth`; gated elements appear only after `/api/auth/me` answers → `findByRole`.
- Work on branch **`feature/crm-8-customer-profiles`** (already created from `main`).
- Phase 1 order: foundation ✅ → security-admin (CRM-6, CRM-7) ✅ → **CRM-8 (this)** → CRM-9 (contact details) → CRM-10 (timeline) → CRM-11 (notes & attachments) → tickets (CRM-12..18) → SLA (CRM-19..22) → email/WhatsApp (CRM-23..26).
- **No new NuGet or npm packages, no new shadcn component** (`table`, `dialog`, `alert-dialog`, `field`, `input`, `button` exist since CRM-3/CRM-6). **dotnet-ef 10.0.8** is installed globally (prints the harmless "older than runtime 10.0.11" warning). One new migration: `AddCustomers`.
- **Shared contract created here** (CRM-9..11 and tickets build on it): the first Domain entity `Crm.Domain.Customers.Customer`, `Crm.Domain.Common.ISoftDeletable`, the named query filter `CrmDbContext.SoftDeleteFilter` (`"SoftDelete"`) + the guard `EverySoftDeletableEntity_HasTheSoftDeleteQueryFilter`, the UTC `DateTime` convention (`UtcDateTimeConverter`), `IEntityTypeConfiguration<T>` classes in `Crm.Infrastructure/Persistence/Configurations/` (picked up by `ApplyConfigurationsFromAssembly`), the **Application service + repository interface** pattern (`CustomerService` in Application, `CustomerRepository` in Infrastructure), `LikePattern`, `PagingText`; client `client/src/api/paging.ts` (`PagedResult`, `ListParams`, `listPath`), `apiDelete`, `client/src/api/customers.ts`.

---

## Story Goal

Agents (every staff role) create, view, edit and remove customer profiles from a "Customers" page, so tickets (CRM-13) can be linked to a known customer.

1. `POST /api/customers` with a name → **201** + `Location: /api/customers/{id}` + the profile (AC 1). Email and phone are optional.
2. `POST /api/customers` without a name (missing, empty or blank) → **400** ProblemDetails with `errors.name` (AC 2). A malformed email / phone → 400 with `errors.email` / `errors.phone`.
3. `GET /api/customers?search=&page=&pageSize=` → `PagedResult<CustomerResponse>`; `search` matches **name, phone or email** (contains, case-insensitive, `%`/`_` literal), ordered by name; `page` default 1, `pageSize` default 20, max 100, out of range → 400 (AC 3).
4. `PUT /api/customers/{id}` replaces the profile → **200**; `UpdatedAt` moves to the current time, `CreatedAt` stays (AC 4). `GET /api/customers/{id}` returns one profile.
5. `DELETE /api/customers/{id}` → **204**, **soft delete**: the row stays with `IsDeleted = 1` + `DeletedAt`, the customer disappears from the list, `GET`/`PUT`/`DELETE` answer 404 (AC 5). Because the row is never removed, tickets (CRM-13) keep their customer.
6. Reads need `customers.view`, writes `customers.manage` (CRM-7 catalogue; every seeded role has both). No token → 401; a user without the permission → 403.
7. Client: `/customers` shows the real page (search, paged table, "Add customer" / "Edit" dialog, "Delete" with confirmation), all text in English and Arabic; add/edit/delete are hidden without `customers.manage`.

**Decisions**

- **Phone and email before CRM-9:** the customer gets **one optional `Email` (max 256) and one optional `Phone` (max 32)** column now — they are the customer's *primary* email and phone. CRM-9 adds the `CustomerContact` list (many phones/emails/WhatsApp, E.164, primary per type) and keeps these two columns as the denormalized primary values, so the list, the search and this page keep working unchanged (migration path in "How later stories build on this" and "Migration / Rollback"). The phone rule is deliberately loose now (optional `+`, ASCII digits, spaces, dashes, brackets, at least 6 digits); CRM-9 owns normalization to E.164. No uniqueness on email/phone (two customers of one company may share a switchboard number; CRM-9 decides lookups).
- **First Domain entity, rules without a database:** `Customer.Create / Update / Delete` take the UTC time as a parameter (`DateTime`, `Kind = Utc` enforced) and hold the rules (name required + trimmed, empty email/phone → `null`, deleted customer cannot be changed, delete is idempotent). Unit-tested in `CustomerTests`.
- **Application service, Infrastructure repository:** CLAUDE.md "Application layer: one service per feature". `UserService` lives in Infrastructure only because it needs ASP.NET Identity; customers have no such reason, so `CustomerService` (validation, clock, Domain calls) lives in `Crm.Application/Customers` and talks to storage through `ICustomerRepository`, implemented with EF Core in `Crm.Infrastructure/Customers/CustomerRepository.cs`. The service is unit-tested with an in-memory fake repository and a hand-set `TimeProvider`; `LayerDependencyTests` keeps Application free of EF Core.
- **Soft delete = `ISoftDeletable` + a *named* EF Core 10 query filter** (`HasQueryFilter(CrmDbContext.SoftDeleteFilter, c => !c.IsDeleted)`). Named, so later queries can switch off only this filter (`IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])`) and later filters (none planned) stay independent. A guard test fails for any `ISoftDeletable` entity without it. No "restore" endpoint.
- **All `DateTime` values are UTC end to end:** a model-wide convention (`ConfigureConventions` → `UtcDateTimeConverter`) marks values read from the database as `DateTimeKind.Utc`; without it the times read back from the database are serialized without `Z` and `GetCustomer_ReturnsTheProfile_WithUtcTimes` fails (verified). It only affects `DateTime` / `DateTime?` properties (Identity uses `DateTimeOffset`): the migration contains nothing but the `Customers` table.
- **One request record for create and edit** (`CustomerRequest(Name, Email, Phone)`; nullable on purpose, like `CreateUserRequest`, so a missing field reaches the validator). Edit is a full replace (PUT).
- **Search** uses `LIKE '%…%'` on `Name`, `Email`, `Phone` (no index can serve a contains-search; there is one index on `Name` for the ordering). Same escaping as `UserService`, extracted to `Crm.Infrastructure/Persistence/LikePattern.cs` (`UserService` is **not** changed in this story).
- **Client UI follows the users page** (CRM-6): same layout, search on submit, `Page x of y` pager, dialog with `showCloseButton={false}` + translated "Cancel", `AlertDialog` for delete. Phone and email inputs/cells are `dir="ltr"` so numbers read correctly in Arabic.

**Not in scope:** several contacts per customer, WhatsApp numbers, E.164 normalization, lookup by phone/email (CRM-9); customer details page, interaction timeline (CRM-10); notes and attachments (CRM-11); tickets and the ticket ↔ customer link (CRM-12..18); restoring deleted customers, merging duplicates, import/export, audit log of who changed a customer.

---

## Context — Read These Files First

1. `CLAUDE.md` — Backend rules (layers, "Domain has no dependency on EF Core or ASP.NET", "All dates stored in UTC … `TimeProvider`", `CancellationToken` passed through) and **Architecture decisions** lines 60–71: *Persistence* (line 63), *Endpoints* (line 68), *Application layer* (line 69), *Pagination* (line 70), *Soft delete* (line 71). Frontend rules (shadcn only, theme colors, logical classes, all strings in `ar` + `en`, API only via `client/src/api`, tests by role/label).
2. `.squad/stories/customer-management/CRM-8/intake.md` — acceptance criteria 1–5, **Out of scope**.
3. [../security-admin/07-story-roles-permissions.md](../security-admin/07-story-roles-permissions.md) lines 40–56 (catalogue) and 671–681 (how later stories build on CRM-7); [../security-admin/06-story-user-management.md](../security-admin/06-story-user-management.md) lines 1379–1386 and the frontend tasks (lines 1390–2399: the users page this story mirrors).
4. `server/src/Crm.Domain/Crm.Domain.csproj` (no package references — keep it that way) and `server/src/Crm.Domain/AssemblyReference.cs` (namespace style).
5. `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` — whole file (21 lines): lines 10–20 `OnModelCreating` with the `ApplicationUser` block (kept as is); the file is replaced in Backend task 4.
6. `server/src/Crm.Infrastructure/DependencyInjection.cs` — lines 1–8 usings, line 17 `services.TryAddSingleton(TimeProvider.System);` (the clock `CustomerService` receives), line 37 `services.AddScoped<IUserService, UserService>();` (register the repository after it).
7. `server/src/Crm.Application/DependencyInjection.cs` — whole file (14 lines): line 11 `AddValidatorsFromAssembly` registers the new validators automatically; the service registration goes after it.
8. `server/src/Crm.Infrastructure/Identity/UserService.cs` — lines 26–52 `ListAsync` (validate → defaults → `Like` search → `CountAsync` → `OrderBy` / `Skip` / `Take` → `PagedResult`), line 24 `LikeEscape`, lines 196–199 `EscapeLike` (copied into `LikePattern`; **do not change `UserService`**).
9. `server/src/Crm.Api/Endpoints/UsersEndpoints.cs` — whole file (48 lines): endpoint style (`[AsParameters]`, `Results.Created`, `.WithName`). `server/src/Crm.Api/Program.cs` line 33 `app.MapUsersEndpoints();` (add `app.MapCustomersEndpoints();` after it).
10. `server/src/Crm.Application/Common/Paging/PagedResult.cs` (12 lines: `PagedResult<T>`, `PagingDefaults`), `server/src/Crm.Application/Users/ListUsersQueryValidator.cs` (13 lines: paging rules), `server/src/Crm.Application/Users/UserText.cs` (text class style), `server/src/Crm.Application/Common/Localization/LocalizedText.cs`.
11. `server/tests/Crm.UnitTests/Localization/LocalizedTextCatalogTests.cs` — lines 3–5 usings, lines 37–43 `Catalog_FindsTheTextClasses` (add `CustomerText`, `PagingText`). `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs` lines 5–9 (Domain/Application must not reference `Microsoft.EntityFrameworkCore` / `Microsoft.AspNetCore`). `server/tests/Crm.UnitTests/Crm.UnitTests.csproj` (references Domain + Application only, no `TimeProvider.Testing` package → the unit tests use a tiny hand-written `TimeProvider`).
12. `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` — line 38 `Time` (`FakeTimeProvider`, `Advance`), lines 82–92 `CreateUserAsync(email, password, params roles)` (no role = a user without permissions), lines 95–100 `CreateClientWithRoleAsync`.
13. `server/tests/Crm.Api.IntegrationTests/Auth/PermissionPolicyTests.cs` — line 15 `SignedInOnlyEndpoints`, lines 24–37 `EveryProtectedApiEndpoint_RequiresAKnownPermission`, lines 39–62 `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` (line 54: it sends `{}` to every POST/PUT → the new endpoints must answer 400/404, never 401/403). **No change** — both pick up `/api/customers` automatically.
14. `server/tests/Crm.Api.IntegrationTests/Users/UserListTests.cs`, `UsersAuthorizationTests.cs`, `Auth/SeedTests.cs` line 34 (resolving `CrmDbContext` in a scope) — patterns the new integration tests copy.
15. `client/src/api/client.ts` lines 79–89 (`apiGet` / `apiPost` / `apiPut`; add `apiDelete` after line 89; line 75: a 204 returns `undefined`). `client/src/api/users.ts` lines 1 and 15–21 (`PagedResult` moves to `client/src/api/paging.ts`), lines 42–49 `listUsers` (query-string building, now `listPath`).
16. `client/src/app/AppRoutes.tsx` — line 5 imports, lines 26–28 (the `customers` route inside `RequirePermission`; replace only the element on line 27).
17. `client/src/pages/users/UsersPage.tsx` (86 lines), `client/src/features/users/UserFormDialog.tsx`, `UserStatusAction.tsx`, `UsersTable.tsx`, `useUsers.ts`, `user-form-schema.ts` — the structure the customer files copy. `client/src/features/auth/Can.tsx` (10 lines).
18. `client/src/pages/users/UsersPage.test.tsx` lines 1–75 (mocks of `@/api/auth` + the API module, `renderPage`, `rowOf`, form helpers).
19. `client/src/test/fake-api.ts` lines 69–73 (`/api/users` branch; add `/api/customers` after it). `client/src/App.layout.test.tsx` line 9 `COMING_SOON_LABELS`, lines 94–103 (`opens the page the user asked for after login` uses `/customers` — keeps passing), lines 123–150.
20. `client/src/i18n/en.json` / `ar.json` — lines 98–104 end of the `users` block (add `customers` after it). `client/src/no-hardcoded-text.test.ts` lines 52–61 (English values longer than 3 characters must not appear quoted in code — **comments included**).
21. `client/src/components/ui/dialog.tsx` line 77 (English sr-only "Close" → use `showCloseButton={false}`). **Never hand-edit** `client/src/components/ui/`.
22. `.claude/skills/vercel-react-best-practices/SKILL.md` — direct imports, no barrel files.

Verified while planning (fresh scratch clone of `main` in the session scratchpad; every file below was written there exactly as printed and every command run): **`dotnet build` 0 warnings / 0 errors; `dotnet test` 252 passed (121 unit, 131 integration); `npm test` 490 passed in 23 files; `npm run build` OK (Vite chunk-size warning only); `npm run lint` exit 0.** Further findings:

- Red states observed exactly as written below (unit: `CS0234 The type or namespace name 'Customers' does not exist in the namespace 'Crm.Application'` / `'Crm.Domain'`; integration: `Failed: 35, Passed: 96`; client: 2 unresolved imports + 1 failing layout test).
- EF Core 10.0.11 has **named** query filters: `EntityTypeBuilder<T>.HasQueryFilter(string, Expression<…>)`, `IReadOnlyEntityType.FindDeclaredQueryFilter(string)`, `IgnoreQueryFilters(IReadOnlyCollection<string>)`.
- `configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>()` also applies to `DateTime?` (`DeletedAt` comes back with `Kind = Utc`). Removing the line makes `GetCustomer_ReturnsTheProfile_WithUtcTimes` and `DeleteCustomer_HidesIt_ButKeepsTheRow` fail.
- The route pattern of `MapGet("")` on the group is **`/api/customers/`** (trailing slash) in `RoutePattern.RawText` — `CustomerEndpoints_NeedViewToRead_AndManageToWrite` uses that key.
- `CreateUserAsync(email, password)` with **no roles** works and that user's token gets 403 on every customer endpoint (the only way to test 403: every seeded role has `customers.view` + `customers.manage`).
- `dotnet ef migrations add AddCustomers …` creates only `CreateTable("Customers")` + `IX_Customers_Name`; the Identity tables are untouched.
- Gated buttons (`<Can>`) render after `getCurrentUser` resolves → the page tests click them with `await …findByRole(...)` (with `getByRole` 2 tests failed while planning). Removing `<Can>` around "Add customer" makes `hides add, edit and delete from a user who may only view customers` fail (checked).
- HTTP smoke on a throw-away LocalDB database (`Crm8SmokeThrowaway`, environment `Smoke`, settings from environment variables — no user-secrets touched; dropped afterwards): migrations `InitialIdentity`, `AddUserIsActive`, `AddCustomers` applied; create → 201 + `Location`; `{"name":""}` with `Accept-Language: ar` → 400 `errors.name = ["'الاسم' لا يجب أن يكون فارغاً."]`; search `+966 50` and `NOUR.EXAMPLE` → 1 item; Arabic name created and found by an Arabic search; PUT → 200 with a later `updatedAt`; DELETE → 204, list empty, GET → 404, row still in the table with `IsDeleted = 1` and `DeletedAt` set. All times end in `Z`.
- Working-tree files are CRLF (`git ls-files --eol`: `i/lf w/crlf`): edit with the editor tools, not `sed` multi-line replacements.

---

## Backend Tasks

All commands run from `server/`. **No new NuGet packages.**

### 1 — Unit tests first (Red)

**Create file: `server/tests/Crm.UnitTests/Customers/CustomerTests.cs`** (Domain rules, no database)

```csharp
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public class CustomerTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Created.AddHours(3);

    [Fact]
    public void Create_TrimsTheProfile_AndSetsIdAndTimestamps()
    {
        var customer = Customer.Create("  Nour Trading  ", " info@nour.example ", " +966 50 123 4567 ", Created);

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Nour Trading", customer.Name);
        Assert.Equal("info@nour.example", customer.Email);
        Assert.Equal("+966 50 123 4567", customer.Phone);
        Assert.Equal(Created, customer.CreatedAt);
        Assert.Equal(Created, customer.UpdatedAt);
        Assert.False(customer.IsDeleted);
        Assert.Null(customer.DeletedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutEmailOrPhone_StoresNull(string? missing)
    {
        var customer = Customer.Create("Walk-in", missing, missing, Created);

        Assert.Null(customer.Email);
        Assert.Null(customer.Phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => Customer.Create(name, null, null, Created));
    }

    [Fact]
    public void Create_WithNonUtcTime_Throws()
    {
        var local = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => Customer.Create("Nour", null, null, local));
    }

    [Fact]
    public void Update_ChangesTheProfileAndUpdatedAt_KeepsCreatedAt()
    {
        var customer = Customer.Create("Nour", "old@nour.example", "0501234567", Created);

        customer.Update(" Nour Trading Co. ", null, "+966501234567", Later);

        Assert.Equal("Nour Trading Co.", customer.Name);
        Assert.Null(customer.Email);
        Assert.Equal("+966501234567", customer.Phone);
        Assert.Equal(Created, customer.CreatedAt);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void Delete_IsSoft_TheCustomerKeepsItsData()
    {
        var customer = Customer.Create("Nour", "info@nour.example", null, Created);
        var id = customer.Id;

        customer.Delete(Later);

        Assert.True(customer.IsDeleted);
        Assert.Equal(Later, customer.DeletedAt);
        Assert.Equal(Later, customer.UpdatedAt);
        Assert.Equal(id, customer.Id);
        Assert.Equal("Nour", customer.Name);
    }

    [Fact]
    public void Delete_Twice_KeepsTheFirstDeletionTime()
    {
        var customer = Customer.Create("Nour", null, null, Created);
        customer.Delete(Later);

        customer.Delete(Later.AddDays(1));

        Assert.Equal(Later, customer.DeletedAt);
    }

    [Fact]
    public void Update_OfADeletedCustomer_Throws()
    {
        var customer = Customer.Create("Nour", null, null, Created);
        customer.Delete(Later);

        Assert.Throws<InvalidOperationException>(() => customer.Update("Other", null, null, Later));
    }
}
```

**Create file: `server/tests/Crm.UnitTests/Customers/CustomerRequestValidatorTests.cs`**

```csharp
using Crm.Application.Customers;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Customers;

public class CustomerRequestValidatorTests
{
    private readonly CustomerRequestValidator _request = new();
    private readonly ListCustomersQueryValidator _list = new();

    [Theory]
    [InlineData("Nour Trading", null, null)]
    [InlineData("Nour Trading", "", "")]
    [InlineData("Nour Trading", "info@nour.example", "+966 50 123-4567")]
    [InlineData("نور للتجارة", "info@nour.example", "(050) 123 4567")]
    public void ValidRequest_HasNoErrors(string name, string? email, string? phone)
    {
        Assert.True(_request.Validate(new CustomerRequest(name, email, phone)).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_WithoutName_ReportsName(string? name)
    {
        var result = _request.Validate(new CustomerRequest(name, null, null));

        var error = Assert.Single(result.Errors);
        Assert.Equal("Name", error.PropertyName);
    }

    [Fact]
    public void Request_WithTooLongValues_ReportsEachField()
    {
        var result = _request.Validate(new CustomerRequest(
            new string('n', 201), new string('e', 252) + "@x.io", "+" + new string('1', 32)));

        Assert.Equal(["Email", "Name", "Phone"], result.Errors.Select(e => e.PropertyName).Distinct().Order());
    }

    [Fact]
    public void Request_WithInvalidEmail_ReportsEmail()
    {
        var result = _request.Validate(new CustomerRequest("Nour", "not-an-email", null));

        Assert.Equal("Email", Assert.Single(result.Errors).PropertyName);
    }

    [Theory]
    [InlineData("call me")]
    [InlineData("050-12a-4567")]
    [InlineData("12345")] // fewer than 6 digits
    [InlineData("++966501234567")]
    [InlineData("٠٥٠١٢٣٤٥٦٧")] // Arabic-Indic digits: CRM-9 normalizes numbers
    public void Request_WithInvalidPhone_ReportsPhone(string phone)
    {
        var result = _request.Validate(new CustomerRequest("Nour", null, phone));

        Assert.Equal("Phone", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Request_InArabic_HasArabicMessages()
    {
        var messages = UiCulture.Use("ar", () => _request
            .Validate(new CustomerRequest("", null, "call me"))
            .Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("'الاسم' لا يجب أن يكون فارغاً.", messages);
        Assert.Contains(
            "أدخل رقم هاتف من 6 أرقام على الأقل، يمكن أن يبدأ بـ + ويحتوي على مسافات أو شرطات أو أقواس.", messages);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void ListQuery_PagingLimits(int? page, int? pageSize, bool valid)
    {
        Assert.Equal(valid, _list.Validate(new ListCustomersQuery(null, page, pageSize)).IsValid);
    }
}
```

**Create file: `server/tests/Crm.UnitTests/Customers/CustomerServiceTests.cs`** (use cases with an in-memory repository and a hand-set clock)

```csharp
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public class CustomerServiceTests
{
    private readonly FakeCustomerRepository _repository = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        _service = new CustomerService(_repository, _clock, new ListCustomersQueryValidator(), new CustomerRequestValidator());
    }

    [Fact]
    public async Task Create_WithAName_SavesTheCustomer_WithTheClockTime()
    {
        var response = await _service.CreateAsync(new CustomerRequest(" Nour ", "info@nour.example", null), CancellationToken.None);

        var saved = Assert.Single(_repository.Customers);
        Assert.Equal(saved.Id, response.Id);
        Assert.Equal("Nour", response.Name);
        Assert.Equal(_clock.UtcNow.UtcDateTime, response.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, response.CreatedAt.Kind);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Create_WithoutAName_ThrowsValidationException_WithTheNameField()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new CustomerRequest("  ", null, null), CancellationToken.None));

        Assert.Equal(["name"], error.Errors.Keys);
        Assert.Empty(_repository.Customers);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_ChangesTheProfile_AndUpdatedAt()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(10);

        var updated = await _service.UpdateAsync(created.Id, new CustomerRequest("Nour Trading", null, "0501234567"),
            CancellationToken.None);

        Assert.Equal("Nour Trading", updated.Name);
        Assert.Equal("0501234567", updated.Phone);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(_clock.UtcNow.UtcDateTime, updated.UpdatedAt);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task UpdateGetAndDelete_OfAnUnknownCustomer_ThrowNotFound()
    {
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(id, new CustomerRequest("Nour", null, null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_IsSoft_AndTheCustomerIsNoLongerFound()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);

        await _service.DeleteAsync(created.Id, CancellationToken.None);

        var row = Assert.Single(_repository.Customers); // still stored
        Assert.True(row.IsDeleted);
        Assert.Equal(_clock.UtcNow.UtcDateTime, row.DeletedAt);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task List_UsesDefaultPaging_AndATrimmedSearch()
    {
        await _service.ListAsync(new ListCustomersQuery("  nour  ", null, null), CancellationToken.None);
        Assert.Equal(("nour", 1, 20), _repository.LastList);

        await _service.ListAsync(new ListCustomersQuery("   ", 3, 50), CancellationToken.None);
        Assert.Equal((null, 3, 50), _repository.LastList);
    }

    [Fact]
    public async Task List_WithInvalidPaging_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(new ListCustomersQuery(null, 0, 101), CancellationToken.None));

        Assert.Equal(["page", "pageSize"], error.Errors.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>In-memory repository: like the EF one, it never returns deleted customers.</summary>
    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];

        public int SaveCount { get; private set; }

        public (string? Search, int Page, int PageSize)? LastList { get; private set; }

        public Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
        {
            LastList = (search, page, pageSize);
            List<Customer> visible = [.. Customers.Where(c => !c.IsDeleted)];
            return Task.FromResult(new PagedResult<Customer>(visible, page, pageSize, visible.Count));
        }

        public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Customers.SingleOrDefault(c => c.Id == id && !c.IsDeleted));

        public void Add(Customer customer) => Customers.Add(customer);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>A clock the test sets by hand (the app uses TimeProvider.System).</summary>
    private sealed class TestClock(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
```

**File: `server/tests/Crm.UnitTests/Localization/LocalizedTextCatalogTests.cs`**

- After line 4 (`using Crm.Application.Common.Localization;`) add:

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
```

- After line 42 (`Assert.Contains(typeof(UserText), TextClasses);`) add:

```csharp
        Assert.Contains(typeof(CustomerText), TextClasses);
        Assert.Contains(typeof(PagingText), TextClasses);
```

Run `dotnet test` → **Red**: compile errors `CS0234: The type or namespace name 'Customers' does not exist in the namespace 'Crm.Application'` (and `'Crm.Domain'`), `CS0246 'CustomerService' / 'ICustomerRepository' / … could not be found`.

### 2 — Domain + Application (Green for unit tests)

**Create file: `server/src/Crm.Domain/Common/ISoftDeletable.cs`**

```csharp
namespace Crm.Domain.Common;

/// <summary>
/// An entity that is never physically deleted: deleting it sets <see cref="IsDeleted"/> and the row stays, so rows
/// that point at it (tickets, notes, ...) stay valid. Crm.Infrastructure hides deleted rows with the global query
/// filter named "SoftDelete" (a test fails for an ISoftDeletable entity without it).
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    /// <summary>When the entity was deleted (UTC); null while it is not deleted.</summary>
    DateTime? DeletedAt { get; }
}
```

**Create file: `server/src/Crm.Domain/Customers/Customer.cs`**

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.Customers;

/// <summary>
/// A customer that tickets belong to. <see cref="Email"/> and <see cref="Phone"/> are the customer's primary contact
/// details (CRM-9 adds more contacts per customer). Times are UTC and come from the caller (the Application layer
/// passes the injected TimeProvider's time), so the rules are testable without a clock or a database.
/// </summary>
public sealed class Customer : ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;

    private Customer()
    {
        // EF Core materializes customers through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    /// <summary>A new customer. Name is required; empty email / phone are stored as null; values are trimmed.</summary>
    public static Customer Create(string name, string? email, string? phone, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var customer = new Customer { Id = Guid.NewGuid(), CreatedAt = utcNow };
        customer.SetProfile(name, email, phone, utcNow);
        return customer;
    }

    /// <summary>Replaces the profile (same rules as <see cref="Create"/>). A deleted customer cannot be changed.</summary>
    public void Update(string name, string? email, string? phone, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted customer cannot be changed.");
        }

        SetProfile(name, email, phone, utcNow);
    }

    /// <summary>Soft delete: the row and its data stay (tickets keep pointing at it). Deleting twice changes nothing.</summary>
    public void Delete(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = utcNow;
        UpdatedAt = utcNow;
    }

    private void SetProfile(string name, string? email, string? phone, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Email = TrimToNull(email);
        Phone = TrimToNull(phone);
        UpdatedAt = utcNow;
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
```

The Domain project stays **without** package references (`LayerDependencyTests.Domain_DoesNotReferenceFrameworkOrOuterLayers`).

**Create file: `server/src/Crm.Application/Common/Paging/PagingText.cs`** (field names of `page` / `pageSize` for every later list; the users validator keeps `UserText` — unchanged)

```csharp
using Crm.Application.Common.Localization;

namespace Crm.Application.Common.Paging;

/// <summary>Field names of the paging query string (<c>page</c>, <c>pageSize</c>), in the request language.</summary>
public static class PagingText
{
    public static string PageField => LocalizedText.Get("Page", "الصفحة");

    public static string PageSizeField => LocalizedText.Get("Page size", "حجم الصفحة");
}
```

**Create file: `server/src/Crm.Application/Customers/CustomerContracts.cs`**

```csharp
namespace Crm.Application.Customers;

/// <summary>GET /api/customers query string: <c>search</c> (name, phone or email), <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record ListCustomersQuery(string? Search, int? Page, int? PageSize);

/// <summary>Body of POST /api/customers and PUT /api/customers/{id}. Only the name is required.</summary>
public sealed record CustomerRequest(string? Name, string? Email, string? Phone);

/// <summary>A customer as the API returns it. <c>CreatedAt</c> / <c>UpdatedAt</c> are UTC.</summary>
public sealed record CustomerResponse(
    Guid Id, string Name, string? Email, string? Phone, DateTime CreatedAt, DateTime UpdatedAt);
```

**Create file: `server/src/Crm.Application/Customers/CustomerText.cs`**

```csharp
using Crm.Application.Common.Localization;

namespace Crm.Application.Customers;

/// <summary>User-facing text of the customer feature, in the request language.</summary>
public static class CustomerText
{
    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string PhoneField => LocalizedText.Get("Phone", "رقم الهاتف");

    public static string PhoneInvalid => LocalizedText.Get(
        "Enter a phone number with at least 6 digits; it may start with + and contain spaces, dashes or brackets.",
        "أدخل رقم هاتف من 6 أرقام على الأقل، يمكن أن يبدأ بـ + ويحتوي على مسافات أو شرطات أو أقواس.");

    public static string NotFound => LocalizedText.Get(
        "The customer was not found.",
        "العميل غير موجود.");
}
```

**Create file: `server/src/Crm.Application/Customers/CustomerRequestValidator.cs`**

```csharp
using System.Text.RegularExpressions;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Create and edit rules: name required; email and phone optional but well-formed when given.</summary>
public sealed partial class CustomerRequestValidator : AbstractValidator<CustomerRequest>
{
    private const int MinPhoneDigits = 6;

    public CustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Customer.NameMaxLength).WithName(_ => CustomerText.NameField);
        RuleFor(x => x.Email).MaximumLength(Customer.EmailMaxLength).EmailAddress().WithName(_ => CustomerText.EmailField)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        // Loose format for now: CRM-9 normalizes phone numbers to E.164.
        RuleFor(x => x.Phone).MaximumLength(Customer.PhoneMaxLength).WithName(_ => CustomerText.PhoneField)
            .Must(BeAPhoneNumber).WithMessage(_ => CustomerText.PhoneInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }

    private static bool BeAPhoneNumber(string? phone)
    {
        var value = phone!.Trim();
        return PhoneCharacters().IsMatch(value) && value.Count(char.IsAsciiDigit) >= MinPhoneDigits;
    }

    /// <summary>Optional leading +, then ASCII digits, spaces, dashes and brackets only.</summary>
    [GeneratedRegex(@"^\+?[0-9 ()\-]+$")]
    private static partial Regex PhoneCharacters();
}
```

`.When(...)` skips the email / phone rules when the value is empty (empty = "no value", stored as `null`). Write this file with the editor (a Bash heredoc mangles `\-` inside the regex).

**Create file: `server/src/Crm.Application/Customers/ListCustomersQueryValidator.cs`**

```csharp
using Crm.Application.Common.Paging;
using FluentValidation;

namespace Crm.Application.Customers;

public sealed class ListCustomersQueryValidator : AbstractValidator<ListCustomersQuery>
{
    public ListCustomersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(PagingDefaults.DefaultPage).WithName(_ => PagingText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => PagingText.PageSizeField);
    }
}
```

**Create file: `server/src/Crm.Application/Customers/ICustomerRepository.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Domain.Customers;

namespace Crm.Application.Customers;

/// <summary>
/// Customer storage (implemented in Crm.Infrastructure with EF Core). Deleted customers are never returned: the
/// EF "SoftDelete" query filter hides them.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// One page of customers whose name, phone or email contains <paramref name="search"/> (case-insensitive,
    /// wildcards taken literally; null = every customer), ordered by name. <c>TotalCount</c> counts every match.
    /// </summary>
    Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>The customer (tracked, so changes are saved by <see cref="SaveChangesAsync"/>), or null.</summary>
    Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(Customer customer);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

**Create file: `server/src/Crm.Application/Customers/ICustomerService.cs`**

```csharp
using Crm.Application.Common.Paging;

namespace Crm.Application.Customers;

/// <summary>
/// Customer profiles (reads need <c>customers.view</c>, writes <c>customers.manage</c> — enforced by the API).
/// Failures: <c>ValidationException</c> 400, <c>NotFoundException</c> 404 (unknown or deleted customer).
/// </summary>
public interface ICustomerService
{
    Task<PagedResult<CustomerResponse>> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken);

    Task<CustomerResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete: the customer disappears from lists and lookups; the row (and later its tickets) stays.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
```

**Create file: `server/src/Crm.Application/Customers/CustomerService.cs`**

```csharp
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Customer use cases: validation, the clock, the Domain rules, storage through <see cref="ICustomerRepository"/>.</summary>
public sealed class CustomerService(
    ICustomerRepository customers,
    TimeProvider timeProvider,
    IValidator<ListCustomersQuery> listValidator,
    IValidator<CustomerRequest> requestValidator) : ICustomerService
{
    public async Task<PagedResult<CustomerResponse>> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        var search = query.Search?.Trim();

        var page = await customers.ListAsync(
            string.IsNullOrEmpty(search) ? null : search,
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);

        return new PagedResult<CustomerResponse>([.. page.Items.Select(ToResponse)], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<CustomerResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await FindAsync(id, cancellationToken));

    public async Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);

        var customer = Customer.Create(request.Name!, request.Email, request.Phone, UtcNow());
        customers.Add(customer);
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customer = await FindAsync(id, cancellationToken);

        customer.Update(request.Name!, request.Email, request.Phone, UtcNow());
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, cancellationToken);

        customer.Delete(UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private async Task<Customer> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await customers.FindAsync(id, cancellationToken) ?? throw new NotFoundException(CustomerText.NotFound);

    private static CustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.Phone, customer.CreatedAt, customer.UpdatedAt);
}
```

`TimeProvider` is a BCL type (`System`), so Application stays free of framework packages.

**File: `server/src/Crm.Application/DependencyInjection.cs`** — replace the whole file:

```csharp
using Crm.Application.Customers;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application;

public static class DependencyInjection
{
    /// <summary>Registers Application-layer services: every FluentValidation validator in this assembly and the feature services.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly, includeInternalTypes: true);
        services.AddScoped<ICustomerService, CustomerService>();
        return services;
    }
}
```

Run `dotnet test` → unit tests **Green: 121 passed** (76 existing + 11 `CustomerTests` + 20 `CustomerRequestValidatorTests` + 7 `CustomerServiceTests` + 7 new `TextProperty_HasEnglishAndArabicText` rows for `CustomerText` (5) and `PagingText` (2)); integration **94 passed** (unchanged).

### 3 — Integration tests (Red)

**Create file: `server/tests/Crm.Api.IntegrationTests/Customers/CustomerBodies.cs`**

```csharp
namespace Crm.Api.IntegrationTests.Customers;

/// <summary>JSON shapes of /api/customers responses, as the client sees them.</summary>
public sealed record CustomerBody(
    Guid Id, string Name, string? Email, string? Phone, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CustomerPageBody(CustomerBody[] Items, int Page, int PageSize, int TotalCount);
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Customers/CustomerManagementTests.cs`** — AC 1, 2, 4, 5:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Customers;

public class CustomerManagementTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string CustomersPath = "/api/customers";

    private Task<HttpClient> AgentClientAsync() => factory.CreateClientWithRoleAsync(Roles.Agent);

    private static async Task<CustomerBody> CreateAsync(HttpClient client, string name, string? email = null, string? phone = null)
    {
        var response = await client.PostAsJsonAsync(CustomersPath, new { name, email, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!;
    }

    [Fact]
    public async Task CreateCustomer_WithAName_Returns201WithLocationAndTheProfile()
    {
        var agent = await AgentClientAsync();

        var response = await agent.PostAsJsonAsync(CustomersPath,
            new { name = "  Nour Trading  ", email = "info@nour.example", phone = "+966 50 123 4567" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerBody>();
        Assert.Equal($"/api/customers/{customer!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Nour Trading", customer.Name);
        Assert.Equal("info@nour.example", customer.Email);
        Assert.Equal("+966 50 123 4567", customer.Phone);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, customer.CreatedAt);
        Assert.Equal(customer.CreatedAt, customer.UpdatedAt);
    }

    [Fact]
    public async Task CreateCustomer_WithOnlyAName_Returns201WithoutEmailAndPhone()
    {
        var agent = await AgentClientAsync();

        var customer = await CreateAsync(agent, "Walk-in customer", email: "", phone: "  ");

        Assert.Null(customer.Email);
        Assert.Null(customer.Phone);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"name":""}""")]
    [InlineData("""{"name":"   ","email":"info@nour.example"}""")]
    public async Task CreateCustomer_WithoutAName_Returns400WithNameError(string body)
    {
        var agent = await AgentClientAsync();

        var response = await agent.PostAsync(CustomersPath, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["name"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidEmailAndPhone_Returns400WithFieldErrors()
    {
        var agent = await AgentClientAsync();

        var response = await agent.PostAsJsonAsync(CustomersPath, new { name = "Nour", email = "not-an-email", phone = "call me" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["email", "phone"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetCustomer_ReturnsTheProfile_WithUtcTimes()
    {
        var agent = await AgentClientAsync();
        var created = await CreateAsync(agent, "Nour", "info@nour.example");

        var response = await agent.GetAsync($"{CustomersPath}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Nour", json.RootElement.GetProperty("name").GetString());
        // Read back from the database: still marked as UTC ("Z"), so browsers show the right local time.
        Assert.EndsWith("Z", json.RootElement.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task GetCustomer_UnknownId_Returns404()
    {
        var agent = await AgentClientAsync();

        var response = await agent.GetAsync($"{CustomersPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("The customer was not found.", problem!.Detail);
    }

    [Fact]
    public async Task UpdateCustomer_ChangesTheProfile_AndUpdatedAt()
    {
        var agent = await AgentClientAsync();
        var created = await CreateAsync(agent, "Nour", "old@nour.example", "0501234567");
        factory.Time.Advance(TimeSpan.FromMinutes(5));

        var response = await agent.PutAsJsonAsync($"{CustomersPath}/{created.Id}",
            new { name = "Nour Trading Co.", email = "", phone = "+966501234567" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await agent.GetFromJsonAsync<CustomerBody>($"{CustomersPath}/{created.Id}");
        Assert.Equal("Nour Trading Co.", updated!.Name);
        Assert.Null(updated.Email);
        Assert.Equal("+966501234567", updated.Phone);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCustomer_WithoutAName_Returns400_AndKeepsTheProfile()
    {
        var agent = await AgentClientAsync();
        var created = await CreateAsync(agent, "Nour");

        var response = await agent.PutAsJsonAsync($"{CustomersPath}/{created.Id}", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var current = await agent.GetFromJsonAsync<CustomerBody>($"{CustomersPath}/{created.Id}");
        Assert.Equal("Nour", current!.Name);
    }

    [Fact]
    public async Task UpdateCustomer_UnknownId_Returns404()
    {
        var agent = await AgentClientAsync();

        var response = await agent.PutAsJsonAsync($"{CustomersPath}/{Guid.NewGuid()}", new { name = "Nobody" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCustomer_HidesIt_ButKeepsTheRow()
    {
        var agent = await AgentClientAsync();
        var tag = $"del{Guid.NewGuid():N}"[..12];
        var created = await CreateAsync(agent, $"{tag} Customer");
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        var deletedAt = factory.Time.GetUtcNow().UtcDateTime;

        var response = await agent.DeleteAsync($"{CustomersPath}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // Gone from the list, from GET, and cannot be edited or deleted again.
        var page = await agent.GetFromJsonAsync<CustomerPageBody>($"{CustomersPath}?search={tag}");
        Assert.Empty(page!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"{CustomersPath}/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await agent.PutAsJsonAsync($"{CustomersPath}/{created.Id}", new { name = "Back" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.DeleteAsync($"{CustomersPath}/{created.Id}")).StatusCode);

        // Soft delete: the row and its data stay in the database, so the customer's tickets keep their customer.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var row = await db.Set<Customer>().IgnoreQueryFilters().SingleAsync(c => c.Id == created.Id);
        Assert.True(row.IsDeleted);
        Assert.Equal(deletedAt, row.DeletedAt);
        Assert.Equal(DateTimeKind.Utc, row.DeletedAt!.Value.Kind); // nullable DateTime is read back as UTC too
        Assert.Equal($"{tag} Customer", row.Name);
    }

    [Fact]
    public async Task DeleteCustomer_UnknownId_Returns404()
    {
        var agent = await AgentClientAsync();

        var response = await agent.DeleteAsync($"{CustomersPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

(`db.Set<Customer>()` instead of `db.Customers`, so the test project compiles before task 4 adds the `DbSet`.)

**Create file: `server/tests/Crm.Api.IntegrationTests/Customers/CustomerListTests.cs`** — AC 3. Each test creates its own customers with a unique tag / phone prefix and searches for it, so tests never see each other's data:

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Customers;

public class CustomerListTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private Task<HttpClient> AgentClientAsync() => factory.CreateClientWithRoleAsync(Roles.Agent);

    /// <summary>
    /// Creates customers "&lt;tag&gt; n" with email "&lt;tag&gt;-n@example.test" and phone "+9665&lt;digits&gt;n"
    /// (unique per test: the database is shared by the tests of the class). Returns the tag and the phone prefix.
    /// </summary>
    private async Task<(string Tag, string PhonePrefix)> CreateCustomersAsync(HttpClient client, int count)
    {
        var tag = $"c{Guid.NewGuid():N}"[..12];
        var phonePrefix = $"+9665{Random.Shared.Next(10_000_000, 99_999_999)}";
        for (var n = 1; n <= count; n++)
        {
            var response = await client.PostAsJsonAsync("/api/customers",
                new { name = $"{tag} {n}", email = $"{tag}-{n}@example.test", phone = $"{phonePrefix}{n}" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        return (tag, phonePrefix);
    }

    [Fact]
    public async Task List_WithoutParameters_ReturnsFirstPageOf20WithTotalCount()
    {
        var agent = await AgentClientAsync();
        await CreateCustomersAsync(agent, 1);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>("/api/customers");

        Assert.Equal(1, page!.Page);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.TotalCount >= 1);
        Assert.NotEmpty(page.Items);
    }

    [Fact]
    public async Task List_SearchByName_ReturnsOnlyMatchingCustomers()
    {
        var agent = await AgentClientAsync();
        var (tag, _) = await CreateCustomersAsync(agent, 3);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag} 2");

        Assert.Equal($"{tag} 2", Assert.Single(page!.Items).Name);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task List_SearchByPhone_ReturnsTheCustomerWithThatNumber()
    {
        var agent = await AgentClientAsync();
        var (tag, phonePrefix) = await CreateCustomersAsync(agent, 2);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>(
            $"/api/customers?search={Uri.EscapeDataString(phonePrefix + "2")}");

        var customer = Assert.Single(page!.Items);
        Assert.Equal($"{tag} 2", customer.Name);
        Assert.Equal($"{phonePrefix}2", customer.Phone);
    }

    [Fact]
    public async Task List_SearchByEmail_IsCaseInsensitive()
    {
        var agent = await AgentClientAsync();
        var (tag, _) = await CreateCustomersAsync(agent, 2);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag.ToUpperInvariant()}-1@EXAMPLE");

        Assert.Equal($"{tag}-1@example.test", Assert.Single(page!.Items).Email);
    }

    [Fact]
    public async Task List_SearchTreatsWildcardsAsText()
    {
        var agent = await AgentClientAsync();
        await CreateCustomersAsync(agent, 1);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>("/api/customers?search=%25");

        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task List_Paginates_OrderedByName_WithTotalCountOfAllMatches()
    {
        var agent = await AgentClientAsync();
        var (tag, _) = await CreateCustomersAsync(agent, 3);

        var first = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag}&page=1&pageSize=2");
        var second = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag}&page=2&pageSize=2");

        Assert.Equal([$"{tag} 1", $"{tag} 2"], first!.Items.Select(c => c.Name));
        Assert.Equal([$"{tag} 3"], second!.Items.Select(c => c.Name));
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
        var agent = await AgentClientAsync();

        var response = await agent.GetAsync($"/api/customers?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Customers/CustomersAuthorizationTests.cs`** — permissions (CRM-7):

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Customers;

public class CustomersAuthorizationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Every /api/customers endpoint (method, path). The id does not need to exist: authorization runs first.</summary>
    public static TheoryData<string, string> Endpoints() => new()
    {
        { "GET", "/api/customers" },
        { "GET", $"/api/customers/{Guid.Empty}" },
        { "POST", "/api/customers" },
        { "PUT", $"/api/customers/{Guid.Empty}" },
        { "DELETE", $"/api/customers/{Guid.Empty}" },
    };

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        // A valid body, so a missing authorization check would show up as 201/404 instead of 401/403.
        Content = method is "POST" or "PUT" ? JsonContent.Create(new { name = "Blocked customer" }) : null,
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task CustomersApi_WithoutToken_Returns401(string method, string path)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task CustomersApi_ForAUserWithoutCustomerPermissions_Returns403(string method, string path)
    {
        // A signed-in user without any role has no permission at all.
        var email = $"norole-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword);
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));

        var response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(Roles.Agent)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Admin)]
    public async Task EveryStaffRole_CanCreateAndListCustomers(string role)
    {
        var client = await factory.CreateClientWithRoleAsync(role);

        var created = await client.PostAsJsonAsync("/api/customers", new { name = $"Created by {role}" });
        var list = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public void CustomerEndpoints_NeedViewToRead_AndManageToWrite()
    {
        // Route patterns as ASP.NET Core stores them: MapGet("") on the group is "/api/customers/".
        var policies = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/customers", StringComparison.Ordinal) == true)
            .ToDictionary(
                e => $"{e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {e.RoutePattern.RawText}",
                e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).Order().ToArray());

        string[] read = [Permissions.CustomersView];
        string[] write = [Permissions.CustomersManage, Permissions.CustomersView];
        Assert.Equal(read, policies["GET /api/customers/"]);
        Assert.Equal(read, policies["GET /api/customers/{id:guid}"]);
        Assert.Equal(write, policies["POST /api/customers/"]);
        Assert.Equal(write, policies["PUT /api/customers/{id:guid}"]);
        Assert.Equal(write, policies["DELETE /api/customers/{id:guid}"]);
        Assert.Equal(5, policies.Count);
    }
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Persistence/SoftDeleteModelTests.cs`** — guard for CLAUDE.md "Soft delete":

```csharp
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Domain.Common;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Persistence;

/// <summary>Guard: every soft-deletable entity is hidden by the named "SoftDelete" query filter (CLAUDE.md).</summary>
public class SoftDeleteModelTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Same value as CrmDbContext.SoftDeleteFilter (written out so this test fails, not breaks the build, without it).</summary>
    private const string SoftDeleteFilter = "SoftDelete";

    [Fact]
    public void EverySoftDeletableEntity_HasTheSoftDeleteQueryFilter()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<CrmDbContext>().Model;

        var softDeletable = model.GetEntityTypes()
            .Where(type => typeof(ISoftDeletable).IsAssignableFrom(type.ClrType))
            .ToList();

        Assert.Contains(softDeletable, type => type.ClrType == typeof(Customer));
        Assert.All(softDeletable, type => Assert.NotNull(type.FindDeclaredQueryFilter(SoftDeleteFilter)));
    }
}
```

Run `dotnet test` → **Red**: unit 121 passed; integration **Failed: 35, Passed: 96** (131 total). Every new test fails (404 instead of 201/200/400/401/403; `KeyNotFoundException` in the metadata test; `Customer` not in the model) except `UpdateCustomer_UnknownId_Returns404` and `DeleteCustomer_UnknownId_Returns404`, which pass already because the route does not exist yet — they guard the real 404 after task 4.

### 4 — Infrastructure + Api (Green)

**File: `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — replace the whole file (the `ApplicationUser` block is unchanged):

```csharp
using Crm.Domain.Customers;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    /// <summary>
    /// Name of the global query filter that hides soft-deleted rows (every <c>ISoftDeletable</c> entity has it).
    /// Read deleted rows on purpose with <c>IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])</c>.
    /// </summary>
    public const string SoftDeleteFilter = "SoftDelete";

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            // Rows that exist when the column is added (e.g. the seeded SuperAdmin) become active.
            user.Property(u => u.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        });

        // Domain entities: one IEntityTypeConfiguration<T> per entity in Persistence/Configurations.
        builder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Every DateTime is UTC (CLAUDE.md). The database keeps no kind, so mark values read back as UTC;
        // otherwise the API would send them without "Z" and browsers would read them as local time.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
```

**Create file: `server/src/Crm.Infrastructure/Persistence/UtcDateTimeConverter.cs`**

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Infrastructure.Persistence;

/// <summary>
/// Stores DateTime values as they are (the Domain only accepts UTC) and marks values read from the database as
/// <see cref="DateTimeKind.Utc"/>. Applied to every DateTime property in <see cref="CrmDbContext.ConfigureConventions"/>.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
```

**Create file: `server/src/Crm.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`**

```csharp
using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> customer)
    {
        customer.ToTable("Customers");
        customer.HasKey(c => c.Id);
        customer.Property(c => c.Id).ValueGeneratedNever(); // set by Customer.Create
        customer.Property(c => c.Name).HasMaxLength(Customer.NameMaxLength).IsRequired();
        customer.Property(c => c.Email).HasMaxLength(Customer.EmailMaxLength);
        customer.Property(c => c.Phone).HasMaxLength(Customer.PhoneMaxLength);
        customer.HasIndex(c => c.Name); // list order

        // Soft delete: deleted customers disappear from every query; their rows (and later their tickets) stay.
        customer.HasQueryFilter(CrmDbContext.SoftDeleteFilter, c => !c.IsDeleted);
    }
}
```

**Create file: `server/src/Crm.Infrastructure/Persistence/LikePattern.cs`**

```csharp
namespace Crm.Infrastructure.Persistence;

/// <summary>"Contains" patterns for <c>EF.Functions.Like</c> in which %, _ and \ typed by the user match literally.</summary>
internal static class LikePattern
{
    public const string EscapeCharacter = "\\";

    /// <summary>"50%_off" → "%50\%\_off%".</summary>
    public static string Contains(string text) =>
        "%" + text.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter)
            .Replace("%", EscapeCharacter + "%")
            .Replace("_", EscapeCharacter + "_") + "%";
}
```

**Create file: `server/src/Crm.Infrastructure/Customers/CustomerRepository.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Customers;

/// <summary>EF Core storage of customers. The "SoftDelete" query filter hides deleted customers from every query here.</summary>
public sealed class CustomerRepository(CrmDbContext db) : ICustomerRepository
{
    public async Task<PagedResult<Customer>> ListAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var customers = db.Customers.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE is case-insensitive on SQL Server (default collation) and on SQLite (ASCII); wildcards are escaped.
            var pattern = LikePattern.Contains(search);
            customers = customers.Where(c =>
                EF.Functions.Like(c.Name, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Email!, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Phone!, pattern, LikePattern.EscapeCharacter));
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        var items = await customers
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Customer>(items, page, pageSize, totalCount);
    }

    public Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(Customer customer) => db.Customers.Add(customer);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
```

**File: `server/src/Crm.Infrastructure/DependencyInjection.cs`**

- After line 1 (`using Crm.Application.Auth;`) add `using Crm.Application.Customers;`.
- Before line 3 (`using Crm.Infrastructure.Identity;`) add `using Crm.Infrastructure.Customers;`.
- After line 37 (`services.AddScoped<IUserService, UserService>();`) add:

```csharp
        services.AddScoped<ICustomerRepository, CustomerRepository>();
```

**Create file: `server/src/Crm.Api/Endpoints/CustomersEndpoints.cs`**

```csharp
using Crm.Application.Auth;
using Crm.Application.Customers;

namespace Crm.Api.Endpoints;

public static class CustomersEndpoints
{
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
    {
        // Every endpoint needs customers.view; the write endpoints also need customers.manage
        // (several RequireAuthorization calls combine with AND). 401 without a valid token.
        var group = app.MapGroup("/api/customers").RequireAuthorization(Permissions.CustomersView);

        group.MapGet("", async ([AsParameters] ListCustomersQuery query, ICustomerService customers,
                    CancellationToken cancellationToken) =>
                Results.Ok(await customers.ListAsync(query, cancellationToken)))
            .WithName("ListCustomers");

        group.MapGet("/{id:guid}", async (Guid id, ICustomerService customers, CancellationToken cancellationToken) =>
                Results.Ok(await customers.GetAsync(id, cancellationToken)))
            .WithName("GetCustomer");

        group.MapPost("", async (CustomerRequest request, ICustomerService customers, CancellationToken cancellationToken) =>
            {
                var customer = await customers.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/customers/{customer.Id}", customer);
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("CreateCustomer");

        group.MapPut("/{id:guid}", async (Guid id, CustomerRequest request, ICustomerService customers,
                    CancellationToken cancellationToken) =>
                Results.Ok(await customers.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("UpdateCustomer");

        group.MapDelete("/{id:guid}", async (Guid id, ICustomerService customers, CancellationToken cancellationToken) =>
            {
                await customers.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("DeleteCustomer");

        return app;
    }
}
```

**File: `server/src/Crm.Api/Program.cs`** — after line 33 (`app.MapUsersEndpoints();`) add `app.MapCustomersEndpoints();`.

Run `dotnet build` → **0 warnings, 0 errors**. Run `dotnet test` → **Green: 252 passed** (121 unit, 131 integration: 94 existing + 13 `CustomerManagementTests` + 9 `CustomerListTests` + 14 `CustomersAuthorizationTests` + 1 `SoftDeleteModelTests`). `PermissionPolicyTests` (unchanged) now also cover the five customer routes.

Optional proof for the UTC convention: comment out the `HaveConversion<UtcDateTimeConverter>()` line, run `dotnet test --filter "FullyQualifiedName~Customers"` → `GetCustomer_ReturnsTheProfile_WithUtcTimes` and `DeleteCustomer_HidesIt_ButKeepsTheRow` fail; restore the line.

### 5 — Migration

From `server/` (same command style as `InitialIdentity` / `AddUserIsActive`):

```bash
dotnet build
dotnet ef migrations add AddCustomers --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations
```

Expected: ends with `Done. To undo this action, use 'ef migrations remove'`; creates `<timestamp>_AddCustomers.cs` + `<timestamp>_AddCustomers.Designer.cs` and updates `CrmDbContextModelSnapshot.cs` (adds only the `Crm.Domain.Customers.Customer` entity). `Up` must be exactly:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "Customers",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
            Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            IsDeleted = table.Column<bool>(type: "bit", nullable: false),
            DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_Customers", x => x.Id);
        });

    migrationBuilder.CreateIndex(
        name: "IX_Customers_Name",
        table: "Customers",
        column: "Name");
}
```

`Down` drops the `Customers` table. If `Up` touches any `AspNet*` table, the model is wrong — run `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api` and fix it first. **Do not** hand-edit migration files. Run `dotnet test` again → 252 passed (tests use `EnsureCreated` on SQLite, the migration is for SQL Server).

### 6 — How later stories build on this (write nothing here; for later planners)

- **CRM-9 (contact details):** add `Crm.Domain/Customers/CustomerContact.cs` (type Phone / Email / WhatsApp, value normalized to E.164 for numbers, `IsPrimary` — one primary per type) as a child of `Customer` (table `CustomerContacts`, FK `CustomerId`, `DeleteBehavior.Restrict`), changed only through `Customer` methods. **Keep `Customer.Email` / `Customer.Phone` as the denormalized primary email / phone** (the aggregate sets them whenever the primary contact of that type changes): `CustomerRepository.ListAsync`, `CustomersTable` and `CustomerRequest` keep working; search extends with `|| c.Contacts.Any(x => EF.Functions.Like(x.Value, …))`. Its migration creates `CustomerContacts` and copies the existing columns with `migrationBuilder.Sql("INSERT INTO CustomerContacts (…) SELECT … FROM Customers WHERE Phone IS NOT NULL …")` as primary contacts (same for `Email`), normalizing phone numbers it can and leaving the rest as entered. Replace the loose phone rule (`CustomerRequestValidator.BeAPhoneNumber`, client `isPhoneNumber`) by the E.164 rule (convert Arabic-Indic digits ٠–٩ first). Lookup by phone/email = an indexed exact match on the normalized contact value (not the `LIKE` search).
- **CRM-10 / CRM-11 (timeline, notes, attachments):** child entities with `CustomerId` FK (`Restrict`), own `IEntityTypeConfiguration<T>` in `Persistence/Configurations`, routes under `/api/customers/{id:guid}/…` in `CustomersEndpoints` (reads `Permissions.CustomersView`, writes `Permissions.CustomersManage`). Check the parent with `ICustomerRepository.FindAsync` → a deleted customer answers 404 (`CustomerText.NotFound`). Soft-deletable children implement `ISoftDeletable` + `HasQueryFilter(CrmDbContext.SoftDeleteFilter, …)` (`EverySoftDeletableEntity_HasTheSoftDeleteQueryFilter` fails otherwise). A details page goes at `customers/:id` next to the `customers` route inside the same `RequirePermission`; the API `GET /api/customers/{id}` already exists (add `getCustomer` to `client/src/api/customers.ts`).
- **Tickets (CRM-13, AC 5 "its tickets remain"):** `Ticket.CustomerId` is a required FK with `OnDelete(DeleteBehavior.Restrict)` (customers are never physically deleted). Because `Customer` has a query filter, EF warns `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` and an `Include(t => t.Customer)` / join would **hide tickets of deleted customers**. Ticket queries that join customers must use `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])` for the customer side (or project the customer name without the filter), and CRM-13 adds the test "a ticket of a soft-deleted customer is still listed and shows its customer". Creating a ticket for a deleted customer → 404 / 400 (the customer is not found through the filter). The customer picker reuses `listCustomers({ search })`.
- **Every new entity:** Domain class with private setters and `Create`/behaviour methods taking `DateTime utcNow` (the Application service passes `timeProvider.GetUtcNow().UtcDateTime`); `DateTime` properties are UTC automatically (`UtcDateTimeConverter` convention — never use `DateTimeOffset` for new columns: SQLite cannot order by it in the tests). Mapping in `Persistence/Configurations/<Entity>Configuration.cs` (no change to `CrmDbContext.OnModelCreating` except a `DbSet` property). One migration per story: `dotnet ef migrations add <Name> --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations`.
- **Every new feature service without Identity:** Application `I<Feature>Service` + `<Feature>Service` (registered in `Crm.Application/DependencyInjection.cs`) + `I<Feature>Repository` implemented in `Crm.Infrastructure/<Feature>/` (registered in `Crm.Infrastructure/DependencyInjection.cs`). Unit-test the service with a fake repository + hand-set `TimeProvider` (`CustomerServiceTests`), the queries with integration tests.
- **Every later search:** `LikePattern.Contains(text)` + `LikePattern.EscapeCharacter` (`Crm.Infrastructure/Persistence/LikePattern.cs`); paging field names from `PagingText`. `UserService` may switch to `LikePattern` in a later clean-up (not required).
- **Client lists:** `PagedResult` / `ListParams` / `listPath` from `client/src/api/paging.ts`; `apiDelete` for DELETE; copy `CustomersPage` / `useCustomers` (query key prefix per feature) for new paged pages.

---

## Frontend Tasks

All commands run from `client/`. **No new npm packages, no new shadcn component.** Follow `vercel-react-best-practices` (direct imports, no barrel files).

### 1 — Tests first (Red)

**Create file: `client/src/api/customers.test.ts`**

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createCustomer, deleteCustomer, listCustomers, updateCustomer } from './customers'

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

describe('customers API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists customers without a query string by default', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 1, pageSize: 20, totalCount: 0 })

    await listCustomers({})

    expect(sent(fetchMock)).toMatchObject({ path: '/api/customers', method: 'GET' })
  })

  it('sends search, page and pageSize in the query string', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 2, pageSize: 10, totalCount: 0 })

    await listCustomers({ search: '+966 50', page: 2, pageSize: 10 })

    expect(sent(fetchMock).path).toBe('/api/customers?search=%2B966+50&page=2&pageSize=10')
  })

  it('creates a customer with POST /api/customers', async () => {
    const fetchMock = fakeFetch(201, { id: 'c1' })
    const request = { name: 'Nour Trading', email: 'info@nour.example', phone: null }

    await createCustomer(request)

    expect(sent(fetchMock)).toEqual({ path: '/api/customers', method: 'POST', body: request })
  })

  it('updates a customer with PUT /api/customers/{id}', async () => {
    const fetchMock = fakeFetch(200, { id: 'c1' })
    const request = { name: 'Nour Trading Co.', email: null, phone: '+966501234567' }

    await updateCustomer('c1', request)

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1', method: 'PUT', body: request })
  })

  it('deletes a customer with DELETE /api/customers/{id} and no body', async () => {
    const fetchMock = fakeFetch(204)

    await deleteCustomer('c1')

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1', method: 'DELETE', body: undefined })
  })
})
```

**Create file: `client/src/pages/customers/CustomersPage.test.tsx`** — AC 1–5 from the user's side (API module mocked, real React Query, real dialogs, real toasts):

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { createCustomer, deleteCustomer, listCustomers, updateCustomer, type Customer } from '@/api/customers'
import { ApiError } from '@/api/errors'
import type { PagedResult } from '@/api/paging'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { CustomersPage } from './CustomersPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/customers', () => ({
  listCustomers: vi.fn(),
  createCustomer: vi.fn(),
  updateCustomer: vi.fn(),
  deleteCustomer: vi.fn(),
}))

/** An agent: may view and manage customers. */
const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage, permissions.ticketsView, permissions.ticketsManage],
}
/** A user who may only look at customers (no seeded role has this today; the UI must still hide the actions). */
const signedInViewer: CurrentUser = { ...signedInAgent, id: '7', permissions: [permissions.customersView] }

const nour: Customer = {
  id: 'c1',
  name: 'Nour Trading',
  email: 'info@nour.example',
  phone: '+966 50 123 4567',
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}
const omar: Customer = {
  id: 'c2',
  name: 'Omar Walk-in',
  email: null,
  phone: null,
  createdAt: '2026-10-02T08:00:00Z',
  updatedAt: '2026-10-02T08:00:00Z',
}

function pageOf(items: Customer[], totalCount = items.length, page = 1): PagedResult<Customer> {
  return { items, page, pageSize: 20, totalCount }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <CustomersPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function rowOf(name: string) {
  return screen.getByRole('row', { name: new RegExp(name) })
}

function fillCustomerForm(dialog: HTMLElement, values: { name?: string; email?: string; phone?: string }) {
  if (values.name !== undefined) fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: values.name } })
  if (values.email !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Email'), { target: { value: values.email } })
  if (values.phone !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Phone'), { target: { value: values.phone } })
}

describe('CustomersPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(listCustomers).mockReset().mockResolvedValue(pageOf([nour, omar]))
    vi.mocked(createCustomer).mockReset()
    vi.mocked(updateCustomer).mockReset()
    vi.mocked(deleteCustomer).mockReset().mockResolvedValue(undefined)
  })

  it('lists customers with phone and email', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Customers' })).toBeInTheDocument()
    const row = await screen.findByRole('row', { name: /Nour Trading/ })
    expect(within(row).getByText('+966 50 123 4567')).toBeInTheDocument()
    expect(within(row).getByText('info@nour.example')).toBeInTheDocument()
    expect(rowOf('Omar Walk-in')).toBeInTheDocument()
    expect(listCustomers).toHaveBeenCalledWith({ search: undefined, page: 1, pageSize: 20 }, expect.anything())
  })

  it('searches by name, phone or email and starts again at page 1', async () => {
    vi.mocked(listCustomers).mockResolvedValue(pageOf([nour, omar], 45))
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(listCustomers).toHaveBeenLastCalledWith({ search: undefined, page: 2, pageSize: 20 }, expect.anything()),
    )
    vi.mocked(listCustomers).mockResolvedValue(pageOf([nour]))

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search by name, phone or email' }), {
      target: { value: ' +966 50 ' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() =>
      expect(listCustomers).toHaveBeenLastCalledWith({ search: '+966 50', page: 1, pageSize: 20 }, expect.anything()),
    )
    await waitFor(() => expect(screen.queryByRole('row', { name: /Omar Walk-in/ })).not.toBeInTheDocument())
  })

  it('shows "No customers found." when nothing matches', async () => {
    vi.mocked(listCustomers).mockResolvedValue(pageOf([]))
    renderPage()

    expect(await screen.findByText('No customers found.')).toBeInTheDocument()
  })

  it('pages through the results', async () => {
    vi.mocked(listCustomers).mockResolvedValue(pageOf([nour, omar], 45))
    renderPage()

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
    expect(listCustomers).toHaveBeenLastCalledWith({ search: undefined, page: 2, pageSize: 20 }, expect.anything())
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('creates a customer with only a name and reloads the list', async () => {
    vi.mocked(createCustomer).mockResolvedValue({ ...omar, id: 'c3', name: 'Lina Store' })
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })

    fireEvent.click(await screen.findByRole('button', { name: 'Add customer' }))
    const dialog = await screen.findByRole('dialog', { name: 'New customer' })
    fillCustomerForm(dialog, { name: '  Lina Store  ' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(createCustomer).toHaveBeenCalledWith({ name: 'Lina Store', email: null, phone: null })
    expect(await screen.findByText('Customer Lina Store was created.')).toBeInTheDocument()
    expect(listCustomers).toHaveBeenCalledTimes(2)
  })

  it('requires a name and checks email and phone before calling the API', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Add customer' }))
    const dialog = await screen.findByRole('dialog', { name: 'New customer' })
    fillCustomerForm(dialog, { name: '   ', email: 'not-an-email', phone: 'call me' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Enter the customer name.')).toBeInTheDocument()
    expect(within(dialog).getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(
      within(dialog).getByText('Enter a phone number with at least 6 digits; it may start with + and contain spaces, dashes or brackets.'),
    ).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
    expect(createCustomer).not.toHaveBeenCalled()
  })

  it('shows the server message next to the field when the API answers 400', async () => {
    vi.mocked(createCustomer).mockRejectedValue(
      new ApiError('POST /api/customers failed with status 400', 400, {
        status: 400,
        errors: { name: ["'Name' must not be empty."] },
      }),
    )
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Add customer' }))
    const dialog = await screen.findByRole('dialog', { name: 'New customer' })
    fillCustomerForm(dialog, { name: 'Nour' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText("'Name' must not be empty.")).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
  })

  it('edits a customer: the dialog starts with the current profile', async () => {
    vi.mocked(updateCustomer).mockResolvedValue({ ...nour, name: 'Nour Trading Co.', email: null })
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })

    fireEvent.click(await within(rowOf('Nour Trading')).findByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit customer' })
    expect(within(dialog).getByLabelText('Name')).toHaveValue('Nour Trading')
    expect(within(dialog).getByLabelText('Phone')).toHaveValue('+966 50 123 4567')
    fillCustomerForm(dialog, { name: 'Nour Trading Co.', email: '' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(updateCustomer).toHaveBeenCalledWith('c1', { name: 'Nour Trading Co.', email: null, phone: '+966 50 123 4567' }),
    )
    expect(await screen.findByText('Customer Nour Trading Co. was saved.')).toBeInTheDocument()
  })

  it('deletes a customer after confirmation', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })

    fireEvent.click(await within(rowOf('Nour Trading')).findByRole('button', { name: 'Delete' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Delete Nour Trading?' })
    expect(within(confirm).getByText('The customer disappears from the list. Their tickets are kept.')).toBeInTheDocument()
    expect(deleteCustomer).not.toHaveBeenCalled()
    fireEvent.click(within(confirm).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleteCustomer).toHaveBeenCalledWith('c1'))
    expect(await screen.findByText('Customer Nour Trading was deleted.')).toBeInTheDocument()
    expect(listCustomers).toHaveBeenCalledTimes(2)
  })

  it('hides add, edit and delete from a user who may only view customers', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInViewer)
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(screen.queryByRole('button', { name: 'Add customer' })).not.toBeInTheDocument()
    expect(within(rowOf('Nour Trading')).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
    expect(within(rowOf('Nour Trading')).queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument()
  })
})
```

("Lina Store", "Nour Trading" … are test data inside a test file; the no-hard-coded-text guard skips `*.test.tsx`.)

**File: `client/src/test/fake-api.ts`** — after the `/api/users` branch (ends line 72) add:

```ts
    if (path === '/api/customers' || path.startsWith('/api/customers?')) {
      const customer = {
        id: 'c1',
        name: 'Nour Trading',
        email: 'info@nour.example',
        phone: '+966 50 123 4567',
        createdAt: '2026-10-01T08:00:00Z',
        updatedAt: '2026-10-01T08:00:00Z',
      }
      return json(200, { items: [customer], page: 1, pageSize: 20, totalCount: 1 })
    }
```

**File: `client/src/App.layout.test.tsx`**

- Line 9: `const COMING_SOON_LABELS = ['Tickets', 'Customers', 'Knowledge base', 'Reports']` → `const COMING_SOON_LABELS = ['Tickets', 'Knowledge base', 'Reports']`.
- Before the test at line 137 (`opens a page for every navigation item …`) add:

```tsx
  it('opens the customers page from the sidebar', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })

    fireEvent.click(await within(navigation).findByRole('link', { name: 'Customers' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Customers' })).toBeInTheDocument()
    expect(await screen.findByRole('row', { name: /Nour Trading/ })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/customers')
    expect(within(navigation).getByRole('link', { name: 'Customers' })).toHaveAttribute('aria-current', 'page')
  })

```

Run `npm test` → **Red**: `Test Files 3 failed | 20 passed (23)`, `Tests 1 failed | 375 passed (376)` — `customers.test.ts` (`Failed to resolve import "./customers"`), `CustomersPage.test.tsx` (`Failed to resolve import "./CustomersPage"`), `opens the customers page from the sidebar` (`Unable to find role="row" and name /Nour Trading/` — still "coming soon").

### 2 — API modules

**File: `client/src/api/client.ts`** — after `apiPut` (ends line 89) add:

```ts

export function apiDelete<T = void>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>('DELETE', path, { signal })
}
```

**Create file: `client/src/api/paging.ts`**

```ts
/** One page of a list (server: PagedResult<T>). */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

/** Query of a paged list endpoint (server: `search`, `page` default 1, `pageSize` default 20, max 100). */
export interface ListParams {
  search?: string
  page?: number
  pageSize?: number
}

/** `path` plus the query string of the given list parameters ("/api/customers?search=x&page=2"). */
export function listPath(path: string, { search, page, pageSize }: ListParams): string {
  const query = new URLSearchParams()
  if (search) query.set('search', search)
  if (page !== undefined) query.set('page', String(page))
  if (pageSize !== undefined) query.set('pageSize', String(pageSize))
  const queryString = query.toString()
  return queryString ? `${path}?${queryString}` : path
}
```

**File: `client/src/api/users.ts`**

- After line 1 (`import { apiGet, apiPost, apiPut } from './client'`) add:

```ts
import type { PagedResult } from './paging'

// PagedResult moved to ./paging (shared by every list); re-exported for the existing imports.
export type { PagedResult }
```

- Delete lines 15–21 (the `/** One page of a list … */` comment and `export interface PagedResult<T> { … }`) and the empty line after them. Nothing else in the file changes (`UsersPage.test.tsx` keeps importing `PagedResult` from `@/api/users`).

**Create file: `client/src/api/customers.ts`**

```ts
import { apiDelete, apiGet, apiPost, apiPut } from './client'
import { listPath, type ListParams, type PagedResult } from './paging'

/** A customer profile (server: CustomerResponse). Times are UTC ISO strings. */
export interface Customer {
  id: string
  name: string
  email: string | null
  phone: string | null
  createdAt: string
  updatedAt: string
}

/** Body of create and edit. Only the name is required; send null for an empty email or phone. */
export interface CustomerRequest {
  name: string
  email: string | null
  phone: string | null
}

/** GET /api/customers: `search` matches name, phone or email. */
export function listCustomers(params: ListParams, signal?: AbortSignal): Promise<PagedResult<Customer>> {
  return apiGet<PagedResult<Customer>>(listPath('/api/customers', params), signal)
}

export function createCustomer(request: CustomerRequest): Promise<Customer> {
  return apiPost<Customer>('/api/customers', request)
}

export function updateCustomer(id: string, request: CustomerRequest): Promise<Customer> {
  return apiPut<Customer>(`/api/customers/${encodeURIComponent(id)}`, request)
}

/** Soft delete on the server: the customer leaves every list; its tickets stay. */
export function deleteCustomer(id: string): Promise<void> {
  return apiDelete(`/api/customers/${encodeURIComponent(id)}`)
}
```

### 3 — Translations

**File: `client/src/i18n/en.json`** — after the `users` block (line 104 `  }`; add a comma after it) add:

```json
  "customers": {
    "description": "Customer profiles that tickets belong to.",
    "add": "Add customer",
    "searchLabel": "Search by name, phone or email",
    "search": "Search",
    "columns": {
      "name": "Name",
      "phone": "Phone",
      "email": "Email",
      "actions": "Actions"
    },
    "edit": "Edit",
    "delete": "Delete",
    "empty": "No customers found.",
    "loading": "Loading customers…",
    "pageInfo": "Page {{page}} of {{pages}}",
    "previous": "Previous",
    "next": "Next",
    "createTitle": "New customer",
    "createDescription": "Only the name is required.",
    "editTitle": "Edit customer",
    "editDescription": "Change the name, phone or email.",
    "name": "Name",
    "phone": "Phone",
    "email": "Email",
    "save": "Save",
    "saving": "Saving…",
    "cancel": "Cancel",
    "nameRequired": "Enter the customer name.",
    "emailInvalid": "Enter a valid email address.",
    "phoneInvalid": "Enter a phone number with at least 6 digits; it may start with + and contain spaces, dashes or brackets.",
    "created": "Customer {{name}} was created.",
    "updated": "Customer {{name}} was saved.",
    "deleteTitle": "Delete {{name}}?",
    "deleteDescription": "The customer disappears from the list. Their tickets are kept.",
    "deleted": "Customer {{name}} was deleted."
  }
```

**File: `client/src/i18n/ar.json`** — same place, same keys:

```json
  "customers": {
    "description": "ملفات العملاء التي ترتبط بها التذاكر.",
    "add": "إضافة عميل",
    "searchLabel": "البحث بالاسم أو رقم الهاتف أو البريد الإلكتروني",
    "search": "بحث",
    "columns": {
      "name": "الاسم",
      "phone": "رقم الهاتف",
      "email": "البريد الإلكتروني",
      "actions": "الإجراءات"
    },
    "edit": "تعديل",
    "delete": "حذف",
    "empty": "لا يوجد عملاء.",
    "loading": "جارٍ تحميل العملاء…",
    "pageInfo": "صفحة {{page}} من {{pages}}",
    "previous": "السابق",
    "next": "التالي",
    "createTitle": "عميل جديد",
    "createDescription": "الاسم هو الحقل الإلزامي الوحيد.",
    "editTitle": "تعديل العميل",
    "editDescription": "غيّر الاسم أو رقم الهاتف أو البريد الإلكتروني.",
    "name": "الاسم",
    "phone": "رقم الهاتف",
    "email": "البريد الإلكتروني",
    "save": "حفظ",
    "saving": "جارٍ الحفظ…",
    "cancel": "إلغاء",
    "nameRequired": "أدخل اسم العميل.",
    "emailInvalid": "أدخل بريداً إلكترونياً صحيحاً.",
    "phoneInvalid": "أدخل رقم هاتف من 6 أرقام على الأقل، يمكن أن يبدأ بـ + ويحتوي على مسافات أو شرطات أو أقواس.",
    "created": "تم إنشاء العميل {{name}}.",
    "updated": "تم حفظ العميل {{name}}.",
    "deleteTitle": "حذف {{name}}؟",
    "deleteDescription": "سيختفي العميل من القائمة. تبقى تذاكره محفوظة.",
    "deleted": "تم حذف العميل {{name}}."
  }
```

`customers.phoneInvalid` is the same text as the server's `CustomerText.PhoneInvalid` on purpose. Do not quote any of these English values in code or comments (`no-hardcoded-text.test.ts`).

### 4 — Feature components and page

**Create file: `client/src/features/customers/customer-form-schema.ts`**

```ts
import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Same rule as the API (CustomerRequestValidator): optional leading +, digits, spaces, dashes, brackets; 6+ digits. */
export function isPhoneNumber(phone: string): boolean {
  return /^\+?[0-9 ()-]+$/.test(phone) && (phone.match(/[0-9]/g)?.length ?? 0) >= 6
}

/** Client-side checks of the customer dialog (the server validates again). Email and phone may stay empty. */
export function createCustomerFormSchema(t: TFunction) {
  return z.object({
    name: z.string().trim().min(1, t('customers.nameRequired')).max(200),
    email: z
      .string()
      .trim()
      .max(256)
      .refine((email) => email === '' || z.email().safeParse(email).success, t('customers.emailInvalid')),
    phone: z
      .string()
      .trim()
      .max(32)
      .refine((phone) => phone === '' || isPhoneNumber(phone), t('customers.phoneInvalid')),
  })
}

export type CustomerFormValues = z.infer<ReturnType<typeof createCustomerFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const customerFormFields = ['name', 'email', 'phone'] as const
```

**Create file: `client/src/features/customers/useCustomers.ts`**

```ts
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { listCustomers } from '@/api/customers'
import type { ListParams } from '@/api/paging'

/** Prefix of every customers query: mutations invalidate it so every page and search reloads. */
export const customersQueryKey = ['customers'] as const

/** One page of GET /api/customers. The previous page stays visible while the next one loads. */
export function useCustomers(params: ListParams) {
  return useQuery({
    queryKey: [...customersQueryKey, params],
    queryFn: ({ signal }) => listCustomers(params, signal),
    placeholderData: keepPreviousData,
  })
}
```

**Create file: `client/src/features/customers/CustomerFormDialog.tsx`**

```tsx
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { createCustomer, updateCustomer, type Customer, type CustomerRequest } from '@/api/customers'
import { isApiError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { createCustomerFormSchema, customerFormFields, type CustomerFormValues } from './customer-form-schema'
import { customersQueryKey } from './useCustomers'

interface CustomerFormDialogProps {
  /** The customer to edit; without it the dialog creates a new customer. */
  customer?: Customer
  onClose: () => void
}

/** Empty email / phone are sent as null (the API stores "no value", not an empty string). */
function toRequest({ name, email, phone }: CustomerFormValues): CustomerRequest {
  return { name, email: email || null, phone: phone || null }
}

/** Create / edit dialog. Mounted only while open (key per customer), so the form always starts from fresh values. */
export function CustomerFormDialog({ customer, onClose }: CustomerFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createCustomerFormSchema(t), [t])
  const form = useForm<CustomerFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: customer?.name ?? '',
      email: customer?.email ?? '',
      phone: customer?.phone ?? '',
    },
  })

  const save = useMutation({
    mutationFn: (values: CustomerFormValues) =>
      customer ? updateCustomer(customer.id, toRequest(values)) : createCustomer(toRequest(values)),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: customersQueryKey })
      toast.success(t(customer ? 'customers.updated' : 'customers.created', { name: saved.name }))
      onClose()
    },
  })

  async function onSubmit(values: CustomerFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400: show the server's field messages (already in the UI language) next to the fields.
      // Every failure also shows a toast (ApiErrorToaster).
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of customerFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t(customer ? 'customers.editTitle' : 'customers.createTitle')}</DialogTitle>
          <DialogDescription>{t(customer ? 'customers.editDescription' : 'customers.createDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="name"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="customer-name">{t('customers.name')}</FieldLabel>
                  <Input {...field} id="customer-name" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="phone"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="customer-phone">{t('customers.phone')}</FieldLabel>
                  <Input
                    {...field}
                    id="customer-phone"
                    type="tel"
                    dir="ltr"
                    autoComplete="off"
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="email"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="customer-email">{t('customers.email')}</FieldLabel>
                  <Input
                    {...field}
                    id="customer-email"
                    type="email"
                    dir="ltr"
                    autoComplete="off"
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('customers.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('customers.saving') : t('customers.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}
```

**Create file: `client/src/features/customers/DeleteCustomerAction.tsx`**

```tsx
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { deleteCustomer, type Customer } from '@/api/customers'
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
import { customersQueryKey } from './useCustomers'

/** Delete button that asks for confirmation first (soft delete on the server: tickets are kept). */
export function DeleteCustomerAction({ customer }: { customer: Customer }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const remove = useMutation({
    mutationFn: () => deleteCustomer(customer.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: customersQueryKey })
      toast.success(t('customers.deleted', { name: customer.name }))
    },
  })

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button variant="outline" size="sm" disabled={remove.isPending}>
          {t('customers.delete')}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t('customers.deleteTitle', { name: customer.name })}</AlertDialogTitle>
          <AlertDialogDescription>{t('customers.deleteDescription')}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{t('customers.cancel')}</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={() => remove.mutate()}>
            {t('customers.delete')}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
```

**Create file: `client/src/features/customers/CustomersTable.tsx`**

```tsx
import { useTranslation } from 'react-i18next'
import type { Customer } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { DeleteCustomerAction } from './DeleteCustomerAction'

interface CustomersTableProps {
  customers: Customer[]
  onEdit: (customer: Customer) => void
}

export function CustomersTable({ customers, onEdit }: CustomersTableProps) {
  const { t } = useTranslation()

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('customers.columns.name')}</TableHead>
          <TableHead>{t('customers.columns.phone')}</TableHead>
          <TableHead>{t('customers.columns.email')}</TableHead>
          <Can permission={permissions.customersManage}>
            <TableHead className="text-end">{t('customers.columns.actions')}</TableHead>
          </Can>
        </TableRow>
      </TableHeader>
      <TableBody>
        {customers.map((customer) => (
          <TableRow key={customer.id}>
            <TableCell className="font-medium">{customer.name}</TableCell>
            {/* Phone numbers and emails read left to right in Arabic too. */}
            <TableCell dir="ltr" className="text-start">
              {customer.phone}
            </TableCell>
            <TableCell dir="ltr" className="text-start">
              {customer.email}
            </TableCell>
            <Can permission={permissions.customersManage}>
              <TableCell>
                <div className="flex justify-end gap-2">
                  <Button variant="outline" size="sm" onClick={() => onEdit(customer)}>
                    {t('customers.edit')}
                  </Button>
                  <DeleteCustomerAction customer={customer} />
                </div>
              </TableCell>
            </Can>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
```

(`text-start`, `text-end`, `justify-end` are logical classes; `theme.test.ts` rejects `text-left` / `text-right` / `ml-*` …)

**Create file: `client/src/pages/customers/CustomersPage.tsx`**

```tsx
import { PlusIcon, SearchIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { Customer } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Can } from '@/features/auth/Can'
import { CustomerFormDialog } from '@/features/customers/CustomerFormDialog'
import { CustomersTable } from '@/features/customers/CustomersTable'
import { useCustomers } from '@/features/customers/useCustomers'

const PAGE_SIZE = 20

type DialogState = { mode: 'create' } | { mode: 'edit'; customer: Customer } | null

/** Customers page: search (name, phone, email), paged table, create / edit dialog, delete with confirmation. */
export function CustomersPage() {
  const { t } = useTranslation()
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [dialog, setDialog] = useState<DialogState>(null)
  const customers = useCustomers({ search: search || undefined, page, pageSize: PAGE_SIZE })

  const totalPages = customers.data ? Math.max(1, Math.ceil(customers.data.totalCount / customers.data.pageSize)) : 1

  function onSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearch(searchText.trim())
    setPage(1)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold">{t('nav.customers')}</h1>
          <p className="text-muted-foreground">{t('customers.description')}</p>
        </div>
        <Can permission={permissions.customersManage}>
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <PlusIcon aria-hidden="true" />
            {t('customers.add')}
          </Button>
        </Can>
      </div>

      <form role="search" className="flex max-w-md gap-2" onSubmit={onSearch}>
        <Input
          type="search"
          aria-label={t('customers.searchLabel')}
          placeholder={t('customers.searchLabel')}
          value={searchText}
          onChange={(event) => setSearchText(event.target.value)}
        />
        <Button type="submit" variant="outline">
          <SearchIcon aria-hidden="true" />
          {t('customers.search')}
        </Button>
      </form>

      {customers.isPending ? (
        <p className="text-muted-foreground">{t('customers.loading')}</p>
      ) : customers.data && customers.data.items.length > 0 ? (
        <CustomersTable
          customers={customers.data.items}
          onEdit={(customer) => setDialog({ mode: 'edit', customer })}
        />
      ) : (
        <p className="text-muted-foreground">{t('customers.empty')}</p>
      )}

      <div className="flex items-center justify-end gap-2">
        <span className="text-sm text-muted-foreground">{t('customers.pageInfo', { page, pages: totalPages })}</span>
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          {t('customers.previous')}
        </Button>
        <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
          {t('customers.next')}
        </Button>
      </div>

      {dialog ? (
        <CustomerFormDialog
          key={dialog.mode === 'edit' ? dialog.customer.id : 'new'}
          customer={dialog.mode === 'edit' ? dialog.customer : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}
```

**File: `client/src/app/AppRoutes.tsx`**

- After line 5 (`import { ComingSoonPage } from '@/pages/coming-soon/ComingSoonPage'`) add `import { CustomersPage } from '@/pages/customers/CustomersPage'`.
- Line 27: `<Route path="customers" element={<ComingSoonPage area="customers" />} />` → `<Route path="customers" element={<CustomersPage />} />` (it stays inside `<RequirePermission permission={permissions.customersView} />`, lines 26–28). `ComingSoonPage` stays (tickets, knowledge base, reports). `navigation.ts` is **not** changed.

Run `npm test` → **Green: 490 passed in 23 files** (375 existing + 5 `customers.test.ts` + 10 `CustomersPage.test.tsx` + 1 `App.layout.test.tsx` + 99 new `translations.test.ts` rows for 33 new keys × 3 checks). Then `npm run build` (OK, chunk-size warning only) and `npm run lint` (exit 0).

---

## Edge Cases & Failure Modes

- **Missing / empty / blank name** → `CustomerRequestValidator` (`NotEmpty` fails on whitespace) → 400 `errors.name` (`CreateCustomer_WithoutAName_Returns400WithNameError` ×3, `Request_WithoutName_ReportsName` ×3). The Domain guard `ArgumentException.ThrowIfNullOrWhiteSpace` is a second line of defence (would be a 500 if the validator were bypassed — `Create_WithoutName_Throws`).
- **Name / email / phone too long** (> 200 / 256 / 32) → 400 on the field (`Request_WithTooLongValues_ReportsEachField`); the columns have the same max lengths (`CustomerConfiguration`), so the DB never truncates.
- **Empty or blank email / phone** → valid, stored as `null` (`Customer.TrimToNull`; `CreateCustomer_WithOnlyAName_Returns201WithoutEmailAndPhone`); the client sends `null` for empty fields (`toRequest` in `CustomerFormDialog.tsx`).
- **Phone with letters, fewer than 6 digits, `++`, or Arabic-Indic digits (٠٥٠…)** → 400 `errors.phone` with `CustomerText.PhoneInvalid` (`Request_WithInvalidPhone_ReportsPhone` ×5); the client shows the same text before calling the API. Arabic-Indic digits are accepted only once CRM-9 normalizes numbers.
- **Search with `%`, `_` or `\`** → escaped by `LikePattern.Contains` → literal (`List_SearchTreatsWildcardsAsText`). Leading/trailing spaces trimmed; blank search = no filter (`List_UsesDefaultPaging_AndATrimmedSearch`).
- **Search for a phone typed differently** (`0501234567` vs stored `050 123 4567`) → no match (contains-search on the stored text). Accepted until CRM-9 stores normalized numbers.
- **Case-insensitive search** → SQL Server default collation (`SQL_Latin1_General_CP1_CI_AS`) and SQLite ASCII `LIKE`; Arabic has no case. Verified with Arabic text on LocalDB (column `nvarchar`).
- **`page` / `pageSize` out of range** → 400 `page` / `pageSize` (`ListCustomersQueryValidator`); non-numeric → minimal-API binding 400; page beyond the last → 200 with empty `items` and the real `totalCount`.
- **Unknown id or a non-GUID id** → 404 `CustomerText.NotFound` / route constraint `{id:guid}` → 404.
- **Deleted customer** → hidden by the `SoftDelete` filter from list, GET, PUT, DELETE (404) — `DeleteCustomer_HidesIt_ButKeepsTheRow`. Deleting twice through the Domain keeps the first `DeletedAt` (`Delete_Twice_KeepsTheFirstDeletionTime`); through the API the second DELETE is 404. `Update` of a deleted customer throws in the Domain (`Update_OfADeletedCustomer_Throws`) — unreachable through the API because the filter hides it first.
- **Concurrent edits** → last write wins (no concurrency token). Accepted for this story.
- **Two customers with the same name / email / phone** → allowed (no unique index); CRM-9 decides duplicate handling.
- **`DateTime` kinds** → Domain rejects non-UTC times (`Create_WithNonUtcTime_Throws`); values read from the DB are marked UTC by `UtcDateTimeConverter` → JSON ends with `Z` (`GetCustomer_ReturnsTheProfile_WithUtcTimes`).
- **Permissions** → no token 401, signed-in user without `customers.view` / `customers.manage` 403 ProblemDetails (`CustomersAuthorizationTests`); every seeded role passes. `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` sends `{}` → POST/PUT answer 400, DELETE a random id 404 — no side effects.
- **Migration on the developer database** → `CreateTable` only; existing data untouched. Applied automatically by `dotnet run` in Development.
- **Client: `/api/auth/me` still loading** → "Add customer", the actions column and the row buttons are not rendered yet (`<Can>`); they appear once permissions load (tests use `findByRole`).
- **Client: 400 on save** → the server's message (already in the UI language) under the field **and** the generic CRM-5 toast (ApiErrorToaster). 404 on edit/delete (customer deleted by someone else) → toast "The requested item was not found."; the dialog stays open on edit; the list refreshes on the next search/page.
- **Client: language switch** → labels re-render; phone/email cells and inputs stay `dir="ltr"`.
- **Client: list fails (network / 500)** → toast + "No customers found.".

---

## Test Plan

1. **Unit (new)** — `server/tests/Crm.UnitTests/Customers/CustomerTests.cs` (Domain, no DB): `Create_TrimsTheProfile_AndSetsIdAndTimestamps` (AC 1), `Create_WithoutEmailOrPhone_StoresNull` ×3, `Create_WithoutName_Throws` ×2 (AC 2), `Create_WithNonUtcTime_Throws`, `Update_ChangesTheProfileAndUpdatedAt_KeepsCreatedAt` (AC 4), `Delete_IsSoft_TheCustomerKeepsItsData` (AC 5), `Delete_Twice_KeepsTheFirstDeletionTime`, `Update_OfADeletedCustomer_Throws` — 11 tests.
2. **Unit (new)** — `server/tests/Crm.UnitTests/Customers/CustomerRequestValidatorTests.cs`: `ValidRequest_HasNoErrors` ×4, `Request_WithoutName_ReportsName` ×3 (AC 2), `Request_WithTooLongValues_ReportsEachField`, `Request_WithInvalidEmail_ReportsEmail`, `Request_WithInvalidPhone_ReportsPhone` ×5, `Request_InArabic_HasArabicMessages`, `ListQuery_PagingLimits` ×5 (AC 3) — 20 tests.
3. **Unit (new)** — `server/tests/Crm.UnitTests/Customers/CustomerServiceTests.cs` (fake repository + clock): `Create_WithAName_SavesTheCustomer_WithTheClockTime` (AC 1), `Create_WithoutAName_ThrowsValidationException_WithTheNameField` (AC 2), `Update_ChangesTheProfile_AndUpdatedAt` (AC 4), `UpdateGetAndDelete_OfAnUnknownCustomer_ThrowNotFound`, `Delete_IsSoft_AndTheCustomerIsNoLongerFound` (AC 5), `List_UsesDefaultPaging_AndATrimmedSearch` (AC 3), `List_WithInvalidPaging_ThrowsValidationException` — 7 tests.
4. **Guard (modified)** — `LocalizedTextCatalogTests.Catalog_FindsTheTextClasses` includes `CustomerText`, `PagingText`; 7 new `TextProperty_HasEnglishAndArabicText` rows.
5. **Integration (new)** — `server/tests/Crm.Api.IntegrationTests/Customers/CustomerManagementTests.cs`: `CreateCustomer_WithAName_Returns201WithLocationAndTheProfile` (AC 1), `CreateCustomer_WithOnlyAName_Returns201WithoutEmailAndPhone` (AC 1), `CreateCustomer_WithoutAName_Returns400WithNameError` ×3 (AC 2), `CreateCustomer_WithInvalidEmailAndPhone_Returns400WithFieldErrors`, `GetCustomer_ReturnsTheProfile_WithUtcTimes`, `GetCustomer_UnknownId_Returns404`, `UpdateCustomer_ChangesTheProfile_AndUpdatedAt` (AC 4), `UpdateCustomer_WithoutAName_Returns400_AndKeepsTheProfile`, `UpdateCustomer_UnknownId_Returns404`, `DeleteCustomer_HidesIt_ButKeepsTheRow` (AC 5), `DeleteCustomer_UnknownId_Returns404` — 13 tests.
6. **Integration (new)** — `.../Customers/CustomerListTests.cs` (AC 3): `List_WithoutParameters_ReturnsFirstPageOf20WithTotalCount`, `List_SearchByName_ReturnsOnlyMatchingCustomers`, `List_SearchByPhone_ReturnsTheCustomerWithThatNumber`, `List_SearchByEmail_IsCaseInsensitive`, `List_SearchTreatsWildcardsAsText`, `List_Paginates_OrderedByName_WithTotalCountOfAllMatches`, `List_WithInvalidPaging_Returns400` ×3 — 9 tests.
7. **Integration (new)** — `.../Customers/CustomersAuthorizationTests.cs`: `CustomersApi_WithoutToken_Returns401` ×5, `CustomersApi_ForAUserWithoutCustomerPermissions_Returns403` ×5, `EveryStaffRole_CanCreateAndListCustomers` ×3, `CustomerEndpoints_NeedViewToRead_AndManageToWrite` — 14 tests.
8. **Guard (new)** — `.../Persistence/SoftDeleteModelTests.cs`: `EverySoftDeletableEntity_HasTheSoftDeleteQueryFilter`.
9. **Unchanged, must stay green (backend)** — `PermissionPolicyTests` (`EveryProtectedApiEndpoint_RequiresAKnownPermission`, `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` — now include `/api/customers`), `ProtectedEndpointTests.EveryApiEndpoint_RequiresAuthorizationOrIsExplicitlyAnonymous`, `LayerDependencyTests` (Domain/Application free of EF Core / ASP.NET), all users/auth tests.
10. **Unit (frontend, new)** — `client/src/api/customers.test.ts`: default list path, query string (`+` encoded), POST create, PUT update, DELETE without body.
11. **Component (frontend, new)** — `client/src/pages/customers/CustomersPage.test.tsx`: `lists customers with phone and email` (AC 3), `searches by name, phone or email and starts again at page 1` (AC 3), `shows "No customers found." when nothing matches`, `pages through the results` (AC 3), `creates a customer with only a name and reloads the list` (AC 1), `requires a name and checks email and phone before calling the API` (AC 2), `shows the server message next to the field when the API answers 400` (AC 2), `edits a customer: the dialog starts with the current profile` (AC 4), `deletes a customer after confirmation` (AC 5), `hides add, edit and delete from a user who may only view customers` (permissions).
12. **App level (modified)** — `client/src/App.layout.test.tsx`: new `opens the customers page from the sidebar`; `COMING_SOON_LABELS` without "Customers". `client/src/test/fake-api.ts`: `/api/customers`.
13. **Guards (unchanged files, more rows)** — `translations.test.ts` (33 new keys), `no-hardcoded-text.test.ts`, `theme.test.ts`, `permissions.test.ts`.
14. **Manual smoke** — Verification step 7.

---

## Migration / Rollback

- **Schema:** migration `AddCustomers` creates table `Customers` (`Id uniqueidentifier PK`, `Name nvarchar(200) NOT NULL`, `Email nvarchar(256) NULL`, `Phone nvarchar(32) NULL`, `CreatedAt`/`UpdatedAt datetime2 NOT NULL`, `IsDeleted bit NOT NULL`, `DeletedAt datetime2 NULL`) + index `IX_Customers_Name`. Applied automatically by `dotnet run` in Development (`Database:StartupAction = Migrate`).
- **Rollback (local DB):** stop the API; from `server/`: `dotnet ef database update AddUserIsActive --project src/Crm.Infrastructure --startup-project src/Crm.Api` (drops `Customers` **and its rows**), then `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api` if the migration is not committed yet.
- **Half-applied state:** `CreateTable` + `CreateIndex` run in the migration transaction — either both exist or nothing changed.
- **Code rollback without DB rollback:** older code ignores the extra table — safe.
- **Path to CRM-9:** CRM-9 adds `CustomerContacts` in a **new** migration and copies `Customers.Email` / `Customers.Phone` into it as primary contacts with `migrationBuilder.Sql(...)`; the two columns stay (denormalized primary values), so no data moves out of `Customers` and this migration is never edited.

---

## Verification Steps

1. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings.
2. **Backend tests:** in `server/` run `dotnet test` — **252 passed** (121 unit, 131 integration), 0 failed.
3. **Frontend tests:** in `client/` run `npm test` — **490 passed** in 23 files.
4. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed (the "chunks larger than 500 kB" message is a warning).
5. **Frontend lint:** in `client/` run `npm run lint` — exit code 0, no warnings, no errors.
6. **Migration check:** `git status` shows exactly `server/src/Crm.Infrastructure/Persistence/Migrations/<timestamp>_AddCustomers.cs`, `<timestamp>_AddCustomers.Designer.cs` and the modified `CrmDbContextModelSnapshot.cs`; `Up` creates only `Customers` + `IX_Customers_Name`.
7. **Manual smoke** (user-secrets from CRM-2 already set):
   - Terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http` (applies `AddCustomers`).
   - Terminal 2: `cd client && npm run dev`, open `http://localhost:5173`, sign in as `admin@crm.local`, click **Customers** → empty page "No customers found.", "Page 1 of 1".
   - **Add customer** → save with an empty name → "Enter the customer name." under Name. Enter "Nour Trading", phone `+966 50 123 4567`, email `info@nour.example` → toast "Customer Nour Trading was created.", row appears. Add "Lina Store" with only a name.
   - Search `+966 50` + Enter → only Nour Trading; search `LINA` → only Lina Store; clear the search + Enter → both.
   - **Edit** Nour Trading → change the name to "Nour Trading Co." → toast "Customer Nour Trading Co. was saved.".
   - **Delete** Lina Store → confirmation "Delete Lina Store?" → **Delete** → toast, row gone. (Optional, SQL Server Object Explorer / `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CustomerSupportCrm -Q "SELECT Name, IsDeleted, DeletedAt FROM Customers"` → Lina Store still there with `IsDeleted = 1`.)
   - Sign in as an Agent (create one under **Users** if needed) → Customers visible with Add / Edit / Delete.
   - Click **العربية** → page, dialog, confirmation and toasts in Arabic, table right-to-left, phone numbers still left-to-right.
8. **Regression:** `git status` shows no changes under `.claude/`, `.mcp.json`, `CLAUDE.md`, `client/src/components/ui/`, `client/src/app/navigation.ts`, `server/src/Crm.Infrastructure/Identity/`; `client/src/api/client.ts` is still the only `fetch` caller (`git grep -n "fetch(" -- client/src ':!*.test.*' ':!client/src/test'`); `git grep -n "Microsoft.EntityFrameworkCore" -- server/src/Crm.Domain server/src/Crm.Application` returns nothing.

---

## Done Criteria

- [ ] `POST /api/customers` with a name → 201 + `Location` (`CreateCustomer_WithAName_Returns201WithLocationAndTheProfile`; UI: `creates a customer with only a name and reloads the list`) — AC 1.
- [ ] Without a name → 400 with `errors.name` (`CreateCustomer_WithoutAName_Returns400WithNameError` ×3; UI: `requires a name …`) — AC 2.
- [ ] `GET /api/customers` paginated (`page`/`pageSize`, default 1/20, max 100) and searchable by name, phone or email (`CustomerListTests`; UI: search + paging tests) — AC 3.
- [ ] `PUT /api/customers/{id}` updates the profile and `UpdatedAt` (`UpdateCustomer_ChangesTheProfile_AndUpdatedAt`; UI: `edits a customer …`) — AC 4.
- [ ] `DELETE /api/customers/{id}` is a soft delete: gone from list/GET, row kept with `IsDeleted`/`DeletedAt` (`DeleteCustomer_HidesIt_ButKeepsTheRow`, `Delete_IsSoft_TheCustomerKeepsItsData`; UI: `deletes a customer after confirmation`) — AC 5.
- [ ] `Customer` in `Crm.Domain` (no EF/ASP.NET reference), rules unit-tested without a DB; `ISoftDeletable` + named `SoftDelete` filter + guard test; UTC `DateTime` convention.
- [ ] Reads require `customers.view`, writes `customers.manage` (`CustomersAuthorizationTests`); `PermissionPolicyTests` green.
- [ ] Migration `AddCustomers` in `Crm.Infrastructure/Persistence/Migrations` (only the `Customers` table).
- [ ] `/customers` shows the real page; every new string in `en.json` and `ar.json`; server texts in `CustomerText` / `PagingText`.
- [ ] No new NuGet / npm packages, no new shadcn component, no hand edits in `client/src/components/ui/`.
- [ ] `dotnet build`, `dotnet test` (252), `npm test` (490), `npm run build`, `npm run lint` all pass.
- [ ] Committed on `feature/crm-8-customer-profiles` with message `CRM-8: customer profiles`.
- [ ] `.squad/plans/customer-management/00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 09.**
