# Story 09 — Customer contact details (Story: CRM-9)

## Prerequisites

- Foundation and security-admin features completed and merged to `main` ([../foundation/00-overview.md](../foundation/00-overview.md), [../security-admin/00-overview.md](../security-admin/00-overview.md)). Binding rules used here: Application exceptions + `ValidateOrThrowAsync` (CRM-5, [../foundation/02-story-global-error-handling.md](../foundation/02-story-global-error-handling.md)); every new text in a `<Feature>Text` class and in both `client/src/i18n/{en,ar}.json`, zod schemas built from `t`, server field errors shown as-is (CRM-4, [../foundation/05-story-i18n-rtl.md](../foundation/05-story-i18n-rtl.md) lines 1716–1725); new endpoints `RequireAuthorization(Permissions.X)`, `<Can>` around actions, page tests mock `@/api/auth` and use `findByRole` for gated elements (CRM-7, [../security-admin/07-story-roles-permissions.md](../security-admin/07-story-roles-permissions.md) lines 40–56 and 671–681 — `customers.manage` is listed for "CRM-9 contacts"); new shadcn components only through `npx shadcn@4.21.2 add <name> -y` (CRM-3, [../foundation/04-story-app-layout.md](../foundation/04-story-app-layout.md) line 1290).
- Story 08 completed: [08-story-customer-profiles.md](08-story-customer-profiles.md) (CRM-8) — merged to `main` (commit `193f2f2`). Its section "6 — How later stories build on this" (lines 1575 and 1578–1580) is **binding** for this story: `CustomerContact` as a child of `Customer` in table `CustomerContacts`, changed only through `Customer` methods; **`Customer.Email` / `Customer.Phone` stay as the denormalized primary email / phone** so the list, the search and the CRM-8 page keep working; the migration copies the existing columns into the contacts with `migrationBuilder.Sql(...)`, normalizing the phones it can; the loose phone rule (`CustomerRequestValidator.BeAPhoneNumber`, client `isPhoneNumber`) is replaced by the E.164 rule (Arabic-Indic digits converted first); lookup = an indexed **exact** match on the normalized value, not the `LIKE` search. Also its "Migration / Rollback" line 2599 ("`AddCustomers` is never edited").
- Work on branch **`feature/crm-9-customer-contacts`** (already created from `main`).
- Phase 1 order: foundation ✅ → security-admin ✅ → CRM-8 ✅ → **CRM-9 (this)** → CRM-10 (interaction timeline) → CRM-11 (notes & attachments) → tickets (CRM-12..18) → SLA (CRM-19..22) → email/WhatsApp (CRM-23..26, they call this story's lookup).
- **One new NuGet package:** `libphonenumber-csharp` **9.0.40** (latest on nuget.org, verified while planning) in `Crm.Application` only — namespace `PhoneNumbers`; Domain stays package-free. **No new npm package.** **One new shadcn component:** `native-select` (a styled plain `<select>`; the CLI writes it with logical classes, no package change). **dotnet-ef 10.0.8** is installed globally (prints the harmless "older than runtime 10.0.11" warning). One new migration: `AddCustomerContacts`.
- **Shared contract created here** (CRM-10/11 and the channel stories build on it): `ContactType`, `CustomerContact`, the contact methods of `Customer`, `ContactValues.TryNormalizePhone` (the one place that turns typed / channel phone numbers into E.164), `ICustomerService.LookupAsync(CustomerLookupQuery)` (the lookup the email / WhatsApp channels call), `ICustomerRepository.FindByContactAsync`, routes `/api/customers/lookup` and `/api/customers/{id}/contacts…`, `CustomerResponse.Contacts`; client `getCustomer`, `useCustomer(id)` (query key `['customers', 'detail', id]`), `CustomerContact` / `ContactType` types.

---

## Story Goal

Agents keep any number of phone numbers, email addresses and WhatsApp numbers per customer, with one primary contact per type, so that incoming emails and WhatsApp messages (CRM-23..26) can be matched to the right customer.

1. `POST /api/customers/{id}/contacts` with `{ "type": "phone", "value": "+966501234567" }` → **201** + `Location: /api/customers/{id}/contacts/{contactId}` + the saved contact (AC 1). Phone and WhatsApp values are stored in **E.164**; what agents type is converted first (`0501234567`, `+966 50 123 4567`, `(050) 123-4567`, Arabic-Indic `٠٥٠١٢٣٤٥٦٧` → `+966501234567`). Emails are stored trimmed and lower-case.
2. An invalid phone (`12345`, `call me`, a number that does not exist) or email (`not-an-email`) → **400** ProblemDetails with `errors.value` (contacts) or `errors.phone` / `errors.email` (customer create/edit, lookup) (AC 2). An unknown type → 400 `errors.type`. The same value twice on one customer → **409** "The customer already has this contact.".
3. Every contact type a customer has keeps **exactly one primary contact** (AC 3): the first contact of a type becomes primary; adding with `"isPrimary": true` or `POST /api/customers/{id}/contacts/{contactId}/primary` (**204**) makes it primary and **unsets the old primary**; removing the primary (`DELETE …/contacts/{contactId}`, **204**) promotes the oldest remaining contact of that type. `Customer.Phone` / `Customer.Email` always equal the primary phone / email.
4. `GET /api/customers/lookup?phone=…` or `?email=…` → **200** with the matching customers (AC 4): exact match on the stored value; a phone matches phone **and** WhatsApp contacts and may be typed in any format (also WhatsApp's `966501234567` without "+"); email is case-insensitive; deleted customers are never returned; no match → `[]`. The same lookup is `ICustomerService.LookupAsync`, which the channel stories call directly.
5. `GET /api/customers` and `GET /api/customers/{id}` return `contacts` (phones, then emails, then WhatsApp; primary first). The list search also finds secondary contacts and a phone typed locally (`0501234567` finds `+966501234567`).
6. `POST/PUT /api/customers` keep their CRM-8 body: `email` / `phone` set the **primary** email / phone contact (phone now validated as a real number and stored as E.164).
7. Reads need `customers.view`, writes `customers.manage`. No token → 401, without the permission → 403.
8. Client: every customers row gets a **Contacts** button that opens a dialog listing the contacts (type, value, "Primary" badge) with **Make primary** / **Remove** per contact and an **Add a contact** form (type select, value, "make primary" checkbox) — actions only with `customers.manage`. All text in English and Arabic; numbers shown left-to-right.

**Decisions**

- **Contacts are owned entities of the `Customer` aggregate** (`OwnsMany` → table `CustomerContacts`, key `Id`, FK `CustomerId`). Owned entities are always loaded with their customer and are hidden together with it by the `SoftDelete` filter — no `Include` to forget, and no `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` (verified: the smoke log has no EF warning). EF makes the ownership FK `ON DELETE CASCADE`; customers are never physically deleted (soft delete), so it never fires. Contacts themselves are **hard-deleted** (a removed phone number is not business history; CLAUDE.md soft delete applies to entities other rows point to — nothing points to a contact).
- **E.164 with libphonenumber in Application, a format guard in Domain.** `ContactValues.TryNormalizePhone` (Application) parses with `PhoneNumberUtil.Parse(value, "SA")`, rejects letters, `++`, extensions and invalid numbers (`IsValidNumber`), and formats `PhoneNumberFormat.E164`. Verified with 9.0.40: `0501234567`, `٠٥٠١٢٣٤٥٦٧`, `۰۵۰١…`, `00966…`, `966501234567` → `+966501234567`; `+966521234567` (no such mobile range), `+9665012345678`, `12345` → invalid; letters are mapped to keypad digits by the library, so the input is pre-checked with `^\+?[\d\s().\-]+$`. The Domain (`CustomerContact.Normalize`) only accepts `^\+[1-9][0-9]{6,14}$` for numbers (no package) and lower-cases emails. Default region `SA` is a constant (`ContactValues.DefaultRegion`); per-tenant regions are out of scope.
- **One primary per type = Domain rule, not a database constraint.** A filtered unique index on `(CustomerId, Type) WHERE IsPrimary = 1` would make "set new primary + unset old" depend on EF's UPDATE order inside one `SaveChanges`; the aggregate enforces the rule instead (`EveryType_HasExactlyOnePrimary_AfterManyChanges`). The database has a unique index on `(CustomerId, Type, Value)` (no duplicate contact per customer) and an index on `(Type, Value)` for the lookup.
- **No uniqueness across customers.** CRM-8 allowed two customers to share a number (a company switchboard); the lookup therefore returns a **list** ordered by name (`Lookup_ReturnsEveryCustomerSharingTheNumber_OrderedByName`). The channel stories decide what to do with 0 / 1 / many matches.
- **Contact type on the wire is a lower-case string** (`"phone"`, `"email"`, `"whatsapp"`), parsed by `ContactValues.TryParseType` (numbers such as `"1"` are rejected) and validated by FluentValidation → `errors.type`, instead of an enum JSON converter (which would fail model binding with a non-field 400). Lower case also keeps the client free of `'Phone'` / `'Email'` literals that `no-hardcoded-text.test.ts` rejects (they are English UI values in `en.json`). In the database the type is stored by name (`"Phone"`, `"Email"`, `"WhatsApp"`, `nvarchar(16)`).
- **No "edit contact value" endpoint.** Changing a number = remove + add (keeps the API to three write endpoints). Making primary is its own endpoint (`POST …/primary`, 204), so "unset the old primary" is one explicit action (AC 3). `PUT /api/customers/{id}` still edits the primary email / phone (CRM-8 dialog): a value the customer already has becomes primary, a new value replaces the primary's value, an empty value removes the primary contact (the next one of that type is promoted).
- **Lookup is a service method first** (`ICustomerService.LookupAsync`) and an endpoint second (`GET /api/customers/lookup`, `customers.view`). Exactly one of `phone` / `email` (both or neither → 400) keeps the meaning unambiguous for callers.
- **Client:** a contacts dialog per row instead of a details page (CRM-10 adds `customers/:id`). It reads the customer again with `useCustomer(id)` (`GET /api/customers/{id}`), so every change (mutations invalidate the `['customers']` prefix) shows at once. The CRM-8 actions column is now visible to every user (the Contacts button is a read); Edit / Delete stay inside `<Can permission={permissions.customersManage}>`.

**Not in scope:** editing a contact's value in place; verifying numbers / emails (OTP, confirmation mail); labels per contact ("work", "home"); uniqueness of a number across customers, merging duplicate customers; a per-tenant default phone region; receiving emails / WhatsApp messages and creating tickets from them (CRM-23..26 — only the lookup they will call); customer details page and interaction timeline (CRM-10); notes and attachments (CRM-11); import/export.

---

## Context — Read These Files First

1. `CLAUDE.md` — Backend rules (layers, "Domain has no dependency on EF Core or ASP.NET", "Business rules … unit tested without a database", ProblemDetails, "Authorization on the API", `TimeProvider`, `CancellationToken`) and **Architecture decisions** lines 60–72: *Persistence* (line 63), *Endpoints* (line 68), *Application layer* (line 69), *Soft delete* (line 71), *Channels* (line 72 — the channels this lookup serves). Frontend rules (shadcn only, theme colors, logical classes, all strings in `ar` + `en`, API only via `client/src/api`, tests by role/label).
2. `.squad/stories/customer-management/CRM-9/intake.md` — acceptance criteria 1–4, **Out of scope**.
3. [08-story-customer-profiles.md](08-story-customer-profiles.md) lines 32–43 (CRM-8 decisions: primary `Email`/`Phone` columns, Application service + repository, soft delete, UTC), lines 1573–1581 ("How later stories build on this" — the CRM-9 bullet is line 1575), lines 2593–2599 (migration path).
4. `server/src/Crm.Domain/Customers/Customer.cs` — whole file (90 lines): lines 12–14 max lengths, lines 25–27 `Email` / `Phone`, lines 38–44 `Create`, lines 47–56 `Update`, lines 72–79 `SetProfile` (today it stores the raw email / phone), lines 83–89 `EnsureUtc`. Replaced in Backend task 2. `server/src/Crm.Domain/Crm.Domain.csproj` — no package references (keep it that way).
5. `server/src/Crm.Application/Customers/` — `CustomerContracts.cs` (11 lines), `CustomerRequestValidator.cs` (32 lines; lines 17–31 the loose phone rule `BeAPhoneNumber` + `PhoneCharacters` regex, replaced), `CustomerService.cs` (70 lines; constructor lines 10–14, `CreateAsync` 33–42, `UpdateAsync` 44–53, `ToResponse` 68–69), `CustomerText.cs` (21 lines; lines 14–16 `PhoneInvalid`, new text), `ICustomerRepository.cs` (24 lines), `ICustomerService.cs` (21 lines). `server/src/Crm.Application/Crm.Application.csproj` lines 7–10 (package references; the new one goes after line 9).
6. `server/src/Crm.Infrastructure/Customers/CustomerRepository.cs` (41 lines; lines 16–24 the `LIKE` search, lines 35–36 `FindAsync`), `server/src/Crm.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` (22 lines; line 20 the named `SoftDelete` filter), `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` lines 30–41 (`ApplyConfigurationsFromAssembly`, the UTC `DateTime` convention — applies to `CustomerContact.CreatedAt` too; **no change** to this file), `server/src/Crm.Infrastructure/Persistence/Migrations/20261006001408_AddCustomers.cs` (never edited).
7. `server/src/Crm.Api/Endpoints/CustomersEndpoints.cs` — whole file (47 lines): line 12 group with `Permissions.CustomersView`, lines 14–17 list, lines 37–43 delete (new endpoints go after it, lookup after the list).
8. `server/src/Crm.Application/Common/Validation/ValidatorExtensions.cs` (errors keys are camelCase property names: `Value` → `value`), `server/src/Crm.Application/Common/Exceptions/ConflictException.cs` (409).
9. `server/tests/Crm.UnitTests/Customers/CustomerTests.cs` (lines 13, 18, 56 use non-E.164 phones), `CustomerRequestValidatorTests.cs` (lines 11–19 valid rows, 50–61 invalid phones — line 55 Arabic-Indic digits become **valid**, 63–73 Arabic message), `CustomerServiceTests.cs` (138 lines; constructor line 16, line 53, fake repository lines 103–129). `server/tests/Crm.UnitTests/Localization/LocalizedTextCatalogTests.cs` picks up the new `CustomerText` properties automatically. `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs` lines 5–9 (assembly `PhoneNumbers` is allowed in Application).
10. `server/tests/Crm.Api.IntegrationTests/Customers/` — `CustomerBodies.cs` (7 lines), `CustomerManagementTests.cs` line 41 (`"+966 50 123 4567"` comes back as E.164 now), `CustomerListTests.cs` lines 13–29 (`CreateCustomersAsync`: line 20 builds 13-digit numbers that are not real → must become valid Saudi mobiles), `CustomersAuthorizationTests.cs` lines 14–21 (`Endpoints()`), 68–87 (`CustomerEndpoints_NeedViewToRead_AndManageToWrite`, line 86 counts 5 endpoints).
11. `server/tests/Crm.Api.IntegrationTests/Auth/PermissionPolicyTests.cs` lines 24–37 and 39–62 (**no change**: they pick up the four new routes; line 54 sends `{}` to every POST/PUT → the contact POST answers 400, `…/primary` 404, the lookup GET 400), lines 113–116 (`ConcretePath`: `{contactId:guid}` gets a GUID). `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` line 38 (`Time`).
12. `client/src/api/customers.ts` (37 lines), `client/src/api/client.ts` lines 79–93 (`apiPost(path, undefined)` sends no body; 204 → `undefined`), `client/src/api/customers.test.ts` (65 lines).
13. `client/src/features/customers/customer-form-schema.ts` (29 lines; lines 4–7 `isPhoneNumber`), `useCustomers.ts` (15 lines), `CustomersTable.tsx` (55 lines; lines 24–26 and 40–49 the `<Can>`-wrapped actions column), `CustomerFormDialog.tsx` (`onSubmit` lines 58–70: 400 field errors — the add-contact form copies it), `DeleteCustomerAction.tsx`; `client/src/pages/customers/CustomersPage.tsx` (93 lines; line 15 `DialogState`, lines 66–69 `CustomersTable`, lines 84–90 the dialog).
14. `client/src/features/users/UserFormDialog.tsx` lines 130–145 (`Checkbox` + `Field orientation="horizontal"` + `onCheckedChange` pattern), `client/src/features/users/UsersTable.tsx` line 41 (`Badge variant="secondary"`), `client/src/components/ui/dialog.tsx` line 62 (`sm:max-w-sm` default width — the contacts dialog passes `className="sm:max-w-2xl"`). **Never hand-edit** `client/src/components/ui/`.
15. `client/src/pages/customers/CustomersPage.test.tsx` lines 1–75 (mocks, `renderPage`, `rowOf`), lines 33–48 (fixtures without `contacts`), lines 163–165 (old phone message). `client/src/test/fake-api.ts` lines 73–83 (`/api/customers`). `client/src/test/setup.ts` lines 23–29 (`ResizeObserver` stub — needed by the Radix checkbox in a form).
16. `client/src/i18n/en.json` / `ar.json` — lines 105–141 the `customers` block (line 135 `phoneInvalid`, line 140 `deleted` = last key). `client/src/no-hardcoded-text.test.ts` lines 52–61 (English values longer than 3 characters must not appear quoted anywhere in app code — **comments included**: `"Make primary"` in a comment fails it, seen while planning).
17. `.claude/skills/vercel-react-best-practices/SKILL.md` — direct imports, no barrel files.

Verified while planning (fresh scratch clone of `main` in the session scratchpad; every file below was written there exactly as printed and every command run): **`dotnet build` 0 warnings / 0 errors; `dotnet test` 384 passed (213 unit, 171 integration); `npm test` 581 passed in 24 files; `npm run build` OK (Vite chunk-size warning only); `npm run lint` exit 0.** Further findings:

- Red states observed exactly as written below (unit: `CS0246 The type or namespace name 'ContactType' / 'CustomerContact' / 'CustomerContactRequestValidator' could not be found`; integration: `Failed: 39, Passed: 132` of 171; client: `Tests 14 failed | 489 passed (503)`).
- libphonenumber-csharp 9.0.40 API used: `PhoneNumberUtil.GetInstance()`, `Parse(string, string)` (throws `NumberParseException`), `IsValidNumber(PhoneNumber)`, `PhoneNumber.HasExtension`, `Format(PhoneNumber, PhoneNumberFormat.E164)`.
- EF Core 10.0.11 translates `types.Contains(x.Type)` (string-converted enum) to `[c0].[Type] IN (@types1, @types2)` on SQL Server and works on SQLite; `OwnsMany` + `HasQueryFilter` on the owner needs no filter on the owned type.
- `dotnet ef migrations add AddCustomerContacts …` creates only `CreateTable("CustomerContacts")` + two indexes; the `Customers` and Identity tables are untouched.
- Data migration tested on a throw-away LocalDB database (`Crm9MigrationThrowaway`, migrated to `AddCustomers`, 9 customers inserted with `+966501234567`, `+966 50 123 4568`, `(050) 123-4569`, `00966 50 123 4570`, `966501234571`, `501234572`, no phone, a soft-deleted customer and a 32-character `0…` number, then `dotnet ef database update --connection …`): every phone became E.164 except `501234572` and the 32-character one (kept as entered, by design), emails trimmed + lower-cased, one primary contact per existing value (deleted customers included), `CreatedAt` = the customer's `CreatedAt`. `database update AddCustomers` (Down) dropped `CustomerContacts` and kept all 10 customers. HTTP smoke on the same database (environment `Smoke`, settings from environment variables — no user-secrets touched): create with `٠٥٥٩٨٧٦٥٤٣` → `+966559876543`; add WhatsApp `966559876544` → 201 `+966559876544`; add phone as primary → old primary unset; duplicate → 409 Arabic detail; `12345` → 400 Arabic `errors.value`; lookup by WhatsApp digits and by upper-case email → the customer; make primary / remove → 204 and `phone` follows; search `0559876545` finds it. The database was dropped afterwards; `CustomerSupportCrm` was never touched.
- **This plan was replayed** in a second fresh clone of `main` by a script that applied every file block and every line edit exactly as written here (line numbers checked against the expected text) and ran every command: the 33 printed files matched the verified implementation byte for byte (line endings aside), `dotnet add package`, `npx shadcn@4.21.2 add native-select -y` (identical file) and `dotnet ef migrations add` produced the expected results, the generated `Up` + data part equals the block in Backend task 5, and the build / test / lint numbers above were reproduced. The migration check script of Backend task 5 was run verbatim and printed the expected rows.
- Working-tree files are CRLF (`git ls-files --eol`: `i/lf w/crlf`): edit with the editor tools, not `sed` multi-line replacements. Write files containing `\-` or `\d` in a regex with the editor (a Bash heredoc can mangle them).

---

## Product rules (from story)

| Area | Before (CRM-8) | After (CRM-9) |
|---|---|---|
| Phone on create / edit | Loose rule (optional `+`, ASCII digits, spaces, dashes, brackets, ≥ 6 digits), stored as typed | A real number (libphonenumber, region `SA` for numbers without country code), Arabic-Indic digits accepted, stored as **E.164** |
| Phone message | "Enter a phone number with at least 6 digits; it may start with + and contain spaces, dashes or brackets." | "Enter a valid phone number, e.g. +966501234567 or 0501234567." (same text on server and client) |
| Email | Stored trimmed, as typed | Stored trimmed and **lower-case** |
| Contacts per customer | One email + one phone column | Any number of phones, emails, WhatsApp numbers; one primary per type; the columns = the primaries |
| Search | `LIKE` on name / email / phone columns | Also every contact value, and a phone typed locally matches its E.164 form |
| Lookup | — | `GET /api/customers/lookup?phone=` / `?email=` (exact match), `ICustomerService.LookupAsync` |

---

## Backend Tasks

All commands run from `server/`.

### 1 — Unit tests first (Red)

**Create file: `server/tests/Crm.UnitTests/Customers/CustomerContactTests.cs`** (Domain rules, no database — AC 1, AC 3)

```csharp
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

/// <summary>Contact rules of the Customer aggregate (CRM-9), without a database.</summary>
public class CustomerContactTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Created.AddHours(3);

    private static CustomerContact Primary(Customer customer, ContactType type) =>
        Assert.Single(customer.Contacts, c => c.Type == type && c.IsPrimary);

    [Fact]
    public void AddContact_PhoneInE164_IsSaved()
    {
        var customer = Customer.Create("Nour", null, null, Created);

        var contact = customer.AddContact(ContactType.Phone, "+966501234567", isPrimary: false, Later);

        Assert.Same(contact, Assert.Single(customer.Contacts));
        Assert.NotEqual(Guid.Empty, contact.Id);
        Assert.Equal(customer.Id, contact.CustomerId);
        Assert.Equal(ContactType.Phone, contact.Type);
        Assert.Equal("+966501234567", contact.Value);
        Assert.Equal(Later, contact.CreatedAt);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void AddContact_StoresManyPhonesEmailsAndWhatsAppNumbers()
    {
        var customer = Customer.Create("Nour", "info@nour.example", "+966501234567", Created);

        customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Later);
        customer.AddContact(ContactType.Email, "sales@nour.example", isPrimary: false, Later);
        customer.AddContact(ContactType.WhatsApp, "+966501234567", isPrimary: false, Later);

        Assert.Equal(5, customer.Contacts.Count);
        Assert.Equal(2, customer.Contacts.Count(c => c.Type == ContactType.Phone));
        Assert.Equal(2, customer.Contacts.Count(c => c.Type == ContactType.Email));
        Assert.Equal("+966501234567", Primary(customer, ContactType.WhatsApp).Value);
    }

    [Fact]
    public void AddContact_FirstOfItsType_BecomesPrimary()
    {
        var customer = Customer.Create("Nour", null, null, Created);

        var phone = customer.AddContact(ContactType.Phone, "+966501234567", isPrimary: false, Later);

        Assert.True(phone.IsPrimary);
        Assert.Equal("+966501234567", customer.Phone);
    }

    [Fact]
    public void AddContact_AsPrimary_UnsetsTheOldPrimary()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var old = Primary(customer, ContactType.Phone);

        var added = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: true, Later);

        Assert.True(added.IsPrimary);
        Assert.False(old.IsPrimary);
        Assert.Equal("+966551234567", customer.Phone);
    }

    [Fact]
    public void AddContact_NotAsPrimary_KeepsTheOldPrimary()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);

        var added = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Later);

        Assert.False(added.IsPrimary);
        Assert.Equal("+966501234567", Primary(customer, ContactType.Phone).Value);
        Assert.Equal("+966501234567", customer.Phone);
    }

    [Fact]
    public void MakeContactPrimary_UnsetsTheOldPrimary_OfThatTypeOnly()
    {
        var customer = Customer.Create("Nour", "info@nour.example", "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created);
        var whatsApp = customer.AddContact(ContactType.WhatsApp, "+966501234567", isPrimary: false, Created);

        customer.MakeContactPrimary(second.Id, Later);

        Assert.Equal(second, Primary(customer, ContactType.Phone));
        Assert.Equal("+966551234567", customer.Phone);
        Assert.True(whatsApp.IsPrimary);
        Assert.Equal("info@nour.example", customer.Email);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void EveryType_HasExactlyOnePrimary_AfterManyChanges()
    {
        var customer = Customer.Create("Nour", "info@nour.example", "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: true, Later);
        customer.AddContact(ContactType.Email, "sales@nour.example", isPrimary: true, Later);
        customer.AddContact(ContactType.WhatsApp, "+966551234567", isPrimary: false, Later);
        customer.MakeContactPrimary(second.Id, Later);
        customer.Update("Nour", "info@nour.example", "+966561234567", Later);

        foreach (var type in customer.Contacts.Select(c => c.Type).Distinct())
        {
            Assert.Single(customer.Contacts, c => c.Type == type && c.IsPrimary);
        }
    }

    [Theory]
    [InlineData("0501234567")] // not E.164: the Application layer normalizes first
    [InlineData("+966 50 123 4567")]
    [InlineData("+0501234567")]
    [InlineData("966501234567")]
    public void AddContact_PhoneNotInE164_Throws(string phone)
    {
        var customer = Customer.Create("Nour", null, null, Created);

        Assert.Throws<ArgumentException>(() => customer.AddContact(ContactType.WhatsApp, phone, isPrimary: false, Later));
        Assert.Empty(customer.Contacts);
    }

    [Fact]
    public void AddContact_Email_IsTrimmedAndLowerCased()
    {
        var customer = Customer.Create("Nour", null, null, Created);

        var email = customer.AddContact(ContactType.Email, "  Sales@Nour.Example ", isPrimary: false, Later);

        Assert.Equal("sales@nour.example", email.Value);
        Assert.Equal("sales@nour.example", customer.Email);
    }

    [Fact]
    public void AddContact_TheSameValueTwice_Throws()
    {
        var customer = Customer.Create("Nour", "info@nour.example", "+966501234567", Created);

        Assert.True(customer.HasContact(ContactType.Email, "INFO@nour.example"));
        Assert.Throws<InvalidOperationException>(() =>
            customer.AddContact(ContactType.Email, "INFO@nour.example", isPrimary: false, Later));
        Assert.Throws<InvalidOperationException>(() =>
            customer.AddContact(ContactType.Phone, "+966501234567", isPrimary: false, Later));
        Assert.Equal(2, customer.Contacts.Count);
    }

    [Fact]
    public void RemoveContact_ThePrimary_PromotesTheOldestRemainingOfThatType()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created.AddMinutes(1));
        customer.AddContact(ContactType.Phone, "+966561234567", isPrimary: false, Created.AddMinutes(2));

        customer.RemoveContact(Primary(customer, ContactType.Phone).Id, Later);

        Assert.Equal(2, customer.Contacts.Count);
        Assert.Equal(second, Primary(customer, ContactType.Phone));
        Assert.Equal("+966551234567", customer.Phone);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void RemoveContact_TheLastOfItsType_ClearsTheCustomerValue()
    {
        var customer = Customer.Create("Nour", "info@nour.example", null, Created);

        customer.RemoveContact(Primary(customer, ContactType.Email).Id, Later);

        Assert.Empty(customer.Contacts);
        Assert.Null(customer.Email);
    }

    [Fact]
    public void MakePrimaryOrRemove_OfAnUnknownContact_Throws()
    {
        var customer = Customer.Create("Nour", null, null, Created);

        Assert.Throws<ArgumentException>(() => customer.MakeContactPrimary(Guid.NewGuid(), Later));
        Assert.Throws<ArgumentException>(() => customer.RemoveContact(Guid.NewGuid(), Later));
    }

    [Fact]
    public void Create_WithEmailAndPhone_AddsThemAsPrimaryContacts()
    {
        var customer = Customer.Create("Nour", " Info@Nour.Example ", "+966501234567", Created);

        Assert.Equal("info@nour.example", Primary(customer, ContactType.Email).Value);
        Assert.Equal("+966501234567", Primary(customer, ContactType.Phone).Value);
        Assert.Equal("info@nour.example", customer.Email);
        Assert.Equal("+966501234567", customer.Phone);
    }

    [Fact]
    public void Update_WithANewPhone_ChangesThePrimaryPhone_AndKeepsTheOtherContacts()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created);
        var primaryId = Primary(customer, ContactType.Phone).Id;

        customer.Update("Nour", null, "+966561234567", Later);

        Assert.Equal(2, customer.Contacts.Count);
        Assert.Equal(primaryId, Primary(customer, ContactType.Phone).Id);
        Assert.Equal("+966561234567", customer.Phone);
        Assert.False(second.IsPrimary);
    }

    [Fact]
    public void Update_WithAnExistingSecondaryPhone_MakesItPrimary()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created);

        customer.Update("Nour", null, "+966551234567", Later);

        Assert.Equal(2, customer.Contacts.Count);
        Assert.Equal(second, Primary(customer, ContactType.Phone));
    }

    [Fact]
    public void Update_WithoutPhone_RemovesThePrimaryPhone_AndPromotesTheNext()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var second = customer.AddContact(ContactType.Phone, "+966551234567", isPrimary: false, Created);

        customer.Update("Nour", null, null, Later);

        Assert.Equal(second, Assert.Single(customer.Contacts));
        Assert.True(second.IsPrimary);
        Assert.Equal("+966551234567", customer.Phone);
    }

    [Fact]
    public void Contacts_OfADeletedCustomer_CannotBeChanged()
    {
        var customer = Customer.Create("Nour", null, "+966501234567", Created);
        var phoneId = Primary(customer, ContactType.Phone).Id;
        customer.Delete(Later);

        Assert.Throws<InvalidOperationException>(() =>
            customer.AddContact(ContactType.Email, "info@nour.example", isPrimary: false, Later));
        Assert.Throws<InvalidOperationException>(() => customer.MakeContactPrimary(phoneId, Later));
        Assert.Throws<InvalidOperationException>(() => customer.RemoveContact(phoneId, Later));
    }
}
```

**Create file: `server/tests/Crm.UnitTests/Customers/ContactValuesTests.cs`** (E.164 normalization — AC 1, AC 2)

```csharp
using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public class ContactValuesTests
{
    [Theory]
    [InlineData("+966501234567", "+966501234567")]
    [InlineData("+966 50 123 4567", "+966501234567")]
    [InlineData("0501234567", "+966501234567")] // no country code: Saudi Arabia
    [InlineData("(050) 123-4567", "+966501234567")]
    [InlineData("00966501234567", "+966501234567")]
    [InlineData("966501234567", "+966501234567")] // WhatsApp sends numbers without "+"
    [InlineData("٠٥٠١٢٣٤٥٦٧", "+966501234567")] // Arabic-Indic digits
    [InlineData("۰۵۰۱۲۳۴۵۶۷", "+966501234567")] // Eastern Arabic-Indic (Persian) digits
    [InlineData("+14155552671", "+14155552671")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    public void TryNormalizePhone_ValidNumber_ReturnsE164(string input, string expected)
    {
        Assert.True(ContactValues.TryNormalizePhone(input, out var e164));
        Assert.Equal(expected, e164);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("call me")]
    [InlineData("050-12a-4567")] // letters
    [InlineData("12345")] // too short
    [InlineData("++966501234567")]
    [InlineData("+9665012345678")] // one digit too many
    [InlineData("+966521234567")] // no such Saudi mobile range
    [InlineData("+966 50 123 4567 ext. 5")] // extensions are not stored
    [InlineData("+0123")]
    [InlineData("+966501234567+966501234567+966501234567")] // longer than 32 characters
    public void TryNormalizePhone_InvalidNumber_ReturnsFalse(string? input)
    {
        Assert.False(ContactValues.TryNormalizePhone(input, out var e164));
        Assert.Null(e164);
    }

    [Theory]
    [InlineData("phone", ContactType.Phone)]
    [InlineData("email", ContactType.Email)]
    [InlineData("whatsapp", ContactType.WhatsApp)]
    [InlineData("WhatsApp", ContactType.WhatsApp)]
    public void TryParseType_KnownName_ReturnsTheType(string name, ContactType expected)
    {
        Assert.True(ContactValues.TryParseType(name, out var type));
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fax")]
    [InlineData("1")] // enum numbers are not accepted
    public void TryParseType_UnknownName_ReturnsFalse(string? name)
    {
        Assert.False(ContactValues.TryParseType(name, out _));
    }

    [Fact]
    public void TypeName_IsTheApiName()
    {
        Assert.Equal(["phone", "email", "whatsapp"],
            new[] { ContactType.Phone, ContactType.Email, ContactType.WhatsApp }.Select(ContactValues.TypeName));
    }
}
```

**Create file: `server/tests/Crm.UnitTests/Customers/CustomerContactValidatorTests.cs`** (AC 2, lookup input)

```csharp
using Crm.Application.Customers;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Customers;

public class CustomerContactValidatorTests
{
    private readonly CustomerContactRequestValidator _contact = new();
    private readonly CustomerLookupQueryValidator _lookup = new();

    [Theory]
    [InlineData("phone", "+966501234567")]
    [InlineData("phone", "050 123 4567")]
    [InlineData("whatsapp", "٠٥٠١٢٣٤٥٦٧")]
    [InlineData("email", "sales@nour.example")]
    public void ValidContact_HasNoErrors(string type, string value)
    {
        Assert.True(_contact.Validate(new CustomerContactRequest(type, value, null)).IsValid);
    }

    [Theory]
    [InlineData("phone", "12345")]
    [InlineData("phone", "call me")]
    [InlineData("whatsapp", "+9665012345678")]
    [InlineData("email", "not-an-email")]
    [InlineData("phone", "")]
    [InlineData("email", null)]
    public void InvalidPhoneOrEmail_ReportsValue(string type, string? value)
    {
        var result = _contact.Validate(new CustomerContactRequest(type, value, true));

        Assert.Equal("Value", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void TooLongEmail_ReportsValue()
    {
        var result = _contact.Validate(new CustomerContactRequest("email", new string('e', 252) + "@x.io", null));

        Assert.Equal("Value", result.Errors.Select(e => e.PropertyName).Distinct().Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fax")]
    public void UnknownType_ReportsType(string? type)
    {
        var result = _contact.Validate(new CustomerContactRequest(type, "+966501234567", null));

        Assert.Equal("Type", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void InvalidContact_InArabic_HasArabicMessages()
    {
        var messages = UiCulture.Use("ar", () => _contact
            .Validate(new CustomerContactRequest("phone", "12345", null))
            .Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Equal(["أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567."], messages);
    }

    [Theory]
    [InlineData("+966501234567", null)]
    [InlineData("0501234567", "")]
    [InlineData(null, "info@nour.example")]
    public void ValidLookup_HasNoErrors(string? phone, string? email)
    {
        Assert.True(_lookup.Validate(new CustomerLookupQuery(phone, email)).IsValid);
    }

    [Theory]
    [InlineData(null, null, "Phone")] // neither
    [InlineData("  ", "", "Phone")]
    [InlineData("+966501234567", "info@nour.example", "Email")] // both
    [InlineData("12345", null, "Phone")]
    [InlineData(null, "not-an-email", "Email")]
    public void InvalidLookup_ReportsTheField(string? phone, string? email, string field)
    {
        var result = _lookup.Validate(new CustomerLookupQuery(phone, email));

        Assert.Equal(field, Assert.Single(result.Errors).PropertyName);
    }
}
```

**File: `server/tests/Crm.UnitTests/Customers/CustomerTests.cs`** — the Domain now only accepts E.164 phone numbers:

- Line 13: `" +966 50 123 4567 "` → `" +966501234567 "`; line 18: `Assert.Equal("+966 50 123 4567", customer.Phone);` → `Assert.Equal("+966501234567", customer.Phone);`.
- Line 56: `Customer.Create("Nour", "old@nour.example", "0501234567", Created);` → `Customer.Create("Nour", "old@nour.example", "+966551234567", Created);`.

**File: `server/tests/Crm.UnitTests/Customers/CustomerRequestValidatorTests.cs`**

- After line 15 (`[InlineData("نور للتجارة", "info@nour.example", "(050) 123 4567")]`) add:

```csharp
    [InlineData("نور للتجارة", null, "٠٥٠١٢٣٤٥٦٧")] // Arabic-Indic digits are normalized (CRM-9)
```

- Lines 53–55 (the `"12345"`, `"++966501234567"` and Arabic-Indic rows) → 

```csharp
    [InlineData("12345")] // too short
    [InlineData("++966501234567")]
    [InlineData("+9665012345678")] // one digit too many (CRM-9: a real number is required)
```

- Lines 71–72 (the old Arabic phone message) → 

```csharp
        Assert.Contains("أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567.", messages);
```

**File: `server/tests/Crm.UnitTests/Customers/CustomerServiceTests.cs`** — replace the whole file (new constructor arguments, line 53 expects E.164, nine new tests, the fake repository implements `FindByContactAsync`):

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
        _service = new CustomerService(_repository, _clock, new ListCustomersQueryValidator(), new CustomerRequestValidator(),
            new CustomerContactRequestValidator(), new CustomerLookupQueryValidator());
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
        Assert.Equal("+966501234567", updated.Phone); // stored in E.164 (CRM-9)
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

    [Fact]
    public async Task Create_ReturnsThePrimaryContacts_WithTheNormalizedPhone()
    {
        var response = await _service.CreateAsync(new CustomerRequest("Nour", "Info@Nour.Example", "050 123 4567"),
            CancellationToken.None);

        Assert.Equal("+966501234567", response.Phone);
        Assert.Equal("info@nour.example", response.Email);
        Assert.Equal(
            [("phone", "+966501234567", true), ("email", "info@nour.example", true)],
            response.Contacts.Select(c => (c.Type, c.Value, c.IsPrimary)));
    }

    [Fact]
    public async Task AddContact_NormalizesThePhoneToE164_AndSaves()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);

        var contact = await _service.AddContactAsync(created.Id, new CustomerContactRequest("whatsapp", "٠٥٠١٢٣٤٥٦٧", null),
            CancellationToken.None);

        Assert.Equal(("whatsapp", "+966501234567", true), (contact.Type, contact.Value, contact.IsPrimary));
        Assert.Equal(contact.Id, Assert.Single(_repository.Customers.Single().Contacts).Id);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task AddContact_WithInvalidPhone_ThrowsValidationException_AndSavesNothing()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddContactAsync(created.Id, new CustomerContactRequest("phone", "12345", null), CancellationToken.None));

        Assert.Equal(["value"], error.Errors.Keys);
        Assert.Empty(_repository.Customers.Single().Contacts);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task AddContact_ThatTheCustomerAlreadyHas_ThrowsConflict()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, "+966501234567"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.AddContactAsync(created.Id, new CustomerContactRequest("phone", "0501234567", null), CancellationToken.None));
    }

    [Fact]
    public async Task MakeContactPrimary_UnsetsTheOldPrimary()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, "+966501234567"), CancellationToken.None);
        var second = await _service.AddContactAsync(created.Id, new CustomerContactRequest("phone", "+966551234567", null),
            CancellationToken.None);

        await _service.MakeContactPrimaryAsync(created.Id, second.Id, CancellationToken.None);

        var customer = await _service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal("+966551234567", customer.Phone);
        Assert.Equal(["+966551234567"], customer.Contacts.Where(c => c.IsPrimary).Select(c => c.Value));
    }

    [Fact]
    public async Task ContactChanges_OfAnUnknownCustomerOrContact_ThrowNotFound()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, null), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.AddContactAsync(
            Guid.NewGuid(), new CustomerContactRequest("phone", "+966501234567", null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.MakeContactPrimaryAsync(created.Id, Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RemoveContactAsync(created.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Lookup_ByPhone_SearchesPhoneAndWhatsApp_WithTheE164Number()
    {
        var created = await _service.CreateAsync(new CustomerRequest("Nour", null, "+966501234567"), CancellationToken.None);

        var found = await _service.LookupAsync(new CustomerLookupQuery("050 123 4567", null), CancellationToken.None);

        Assert.Equal(created.Id, Assert.Single(found).Id);
        Assert.Equal([ContactType.Phone, ContactType.WhatsApp], _repository.LastLookup?.Types);
        Assert.Equal("+966501234567", _repository.LastLookup?.Value);
    }

    [Fact]
    public async Task Lookup_ByEmail_SearchesEmails_InLowerCase()
    {
        await _service.LookupAsync(new CustomerLookupQuery(null, " Info@Nour.Example "), CancellationToken.None);

        Assert.Equal([ContactType.Email], _repository.LastLookup?.Types);
        Assert.Equal("info@nour.example", _repository.LastLookup?.Value);
    }

    [Fact]
    public async Task Lookup_WithoutPhoneOrEmail_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.LookupAsync(new CustomerLookupQuery(null, null), CancellationToken.None));

        Assert.Equal(["phone"], error.Errors.Keys);
        Assert.Null(_repository.LastLookup);
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

        public (ContactType[] Types, string Value)? LastLookup { get; private set; }

        public Task<IReadOnlyList<Customer>> FindByContactAsync(
            IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken)
        {
            LastLookup = ([.. types], value);
            IReadOnlyList<Customer> found = [.. Customers.Where(c => !c.IsDeleted
                && c.Contacts.Any(x => types.Contains(x.Type) && x.Value == value))];
            return Task.FromResult(found);
        }

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

Run `dotnet test tests/Crm.UnitTests` → **Red**: compile errors `CS0246: The type or namespace name 'ContactType' could not be found`, `… 'CustomerContact' …`, `… 'CustomerContactRequestValidator' …`, `… 'CustomerLookupQueryValidator' …`.

### 2 — Domain, Application, Infrastructure (Green for unit tests)

Add the package (writes one line after line 9 of `server/src/Crm.Application/Crm.Application.csproj`):

```bash
dotnet add src/Crm.Application package libphonenumber-csharp --version 9.0.40
```

Expected line: `<PackageReference Include="libphonenumber-csharp" Version="9.0.40" />`.

**Create file: `server/src/Crm.Domain/Customers/ContactType.cs`**

```csharp
namespace Crm.Domain.Customers;

/// <summary>Kind of a <see cref="CustomerContact"/>. Stored by name ("Phone", "Email", "WhatsApp").</summary>
public enum ContactType
{
    /// <summary>A phone number in E.164 format ("+966501234567").</summary>
    Phone = 1,

    /// <summary>An email address, stored trimmed and in lower case.</summary>
    Email = 2,

    /// <summary>A WhatsApp number in E.164 format.</summary>
    WhatsApp = 3,
}
```

**Create file: `server/src/Crm.Domain/Customers/CustomerContact.cs`** (write with the editor — regex)

```csharp
using System.Text.RegularExpressions;

namespace Crm.Domain.Customers;

/// <summary>
/// One phone number, email address or WhatsApp number of a customer. Part of the <see cref="Customer"/> aggregate:
/// added, made primary and removed only through <see cref="Customer"/> methods, which keep exactly one primary
/// contact per type (as long as the customer has a contact of that type).
/// </summary>
public sealed partial class CustomerContact
{
    /// <summary>Longest stored value (an email address; E.164 numbers have at most 16 characters).</summary>
    public const int ValueMaxLength = 256;

    private CustomerContact()
    {
        // EF Core materializes contacts through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public ContactType Type { get; private set; }

    /// <summary>Phone / WhatsApp: E.164 ("+966501234567"). Email: trimmed, lower case.</summary>
    public string Value { get; private set; } = string.Empty;

    public bool IsPrimary { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// The stored form of a value: emails are trimmed and lower-cased; phone and WhatsApp numbers must already be in
    /// E.164 format (the Application layer converts what people type) and are only trimmed. Throws
    /// <see cref="ArgumentException"/> for anything else.
    /// </summary>
    public static string Normalize(ContactType type, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        switch (type)
        {
            case ContactType.Email:
                if (trimmed.Length > ValueMaxLength || !trimmed.Contains('@'))
                {
                    throw new ArgumentException("The value is not an email address.", nameof(value));
                }

                return trimmed.ToLowerInvariant();
            case ContactType.Phone:
            case ContactType.WhatsApp:
                if (!E164().IsMatch(trimmed))
                {
                    throw new ArgumentException("Phone numbers must be in E.164 format, e.g. +966501234567.", nameof(value));
                }

                return trimmed;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown contact type.");
        }
    }

    internal static CustomerContact Create(Guid customerId, ContactType type, string normalizedValue, DateTime utcNow) =>
        new() { Id = Guid.NewGuid(), CustomerId = customerId, Type = type, Value = normalizedValue, CreatedAt = utcNow };

    internal void ChangeValue(string normalizedValue) => Value = normalizedValue;

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    /// <summary>"+" and 7 to 15 digits, the first not 0 (ITU-T E.164).</summary>
    [GeneratedRegex(@"^\+[1-9][0-9]{6,14}$")]
    private static partial Regex E164();
}
```

**File: `server/src/Crm.Domain/Customers/Customer.cs`** — replace the whole file (CRM-8 members unchanged except `SetProfile`; `Update` now uses `EnsureNotDeleted`, same message):

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.Customers;

/// <summary>
/// A customer that tickets belong to, with any number of phone numbers, email addresses and WhatsApp numbers
/// (<see cref="Contacts"/>). Every contact type the customer has keeps exactly one primary contact;
/// <see cref="Email"/> and <see cref="Phone"/> are copies of the primary email / phone (kept in sync here) so lists and
/// searches need no join. Times are UTC and come from the caller (the Application layer passes the injected
/// TimeProvider's time), so the rules are testable without a clock or a database.
/// </summary>
public sealed class Customer : ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;

    private readonly List<CustomerContact> _contacts = [];

    private Customer()
    {
        // EF Core materializes customers through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The primary email contact's value (null when the customer has no email).</summary>
    public string? Email { get; private set; }

    /// <summary>The primary phone contact's value, E.164 (null when the customer has no phone).</summary>
    public string? Phone { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public IReadOnlyList<CustomerContact> Contacts => _contacts;

    /// <summary>
    /// A new customer. Name is required and trimmed; a given email / phone (E.164) becomes the primary contact of
    /// that type; empty values add nothing.
    /// </summary>
    public static Customer Create(string name, string? email, string? phone, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var customer = new Customer { Id = Guid.NewGuid(), CreatedAt = utcNow };
        customer.SetProfile(name, email, phone, utcNow);
        return customer;
    }

    /// <summary>
    /// Replaces the profile. Email / phone set the primary contact of that type: a value the customer already has
    /// becomes primary, a new value replaces the primary's value, an empty value removes the primary contact (the
    /// oldest other contact of that type becomes primary). Other contacts stay. A deleted customer cannot be changed.
    /// </summary>
    public void Update(string name, string? email, string? phone, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
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

    /// <summary>True when the customer already has this value (compared in its stored form) for the type.</summary>
    public bool HasContact(ContactType type, string value) =>
        FindContact(type, CustomerContact.Normalize(type, value)) is not null;

    /// <summary>
    /// Adds a contact (phone / WhatsApp values must be E.164). It becomes primary when <paramref name="isPrimary"/> is
    /// true (the old primary of that type is unset) or when it is the first contact of its type. Throws
    /// <see cref="InvalidOperationException"/> when the customer already has the value.
    /// </summary>
    public CustomerContact AddContact(ContactType type, string value, bool isPrimary, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        var normalized = CustomerContact.Normalize(type, value);
        if (FindContact(type, normalized) is not null)
        {
            throw new InvalidOperationException("The customer already has this contact.");
        }

        var contact = CustomerContact.Create(Id, type, normalized, utcNow);
        _contacts.Add(contact);
        if (isPrimary || PrimaryContact(type) is null)
        {
            MakePrimary(contact);
        }

        UpdatedAt = utcNow;
        return contact;
    }

    /// <summary>Makes the contact the primary one of its type; the old primary of that type is unset.</summary>
    public void MakeContactPrimary(Guid contactId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        MakePrimary(GetContact(contactId));
        UpdatedAt = utcNow;
    }

    /// <summary>Removes the contact. When it was primary, the oldest other contact of that type becomes primary.</summary>
    public void RemoveContact(Guid contactId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        Remove(GetContact(contactId));
        UpdatedAt = utcNow;
    }

    private void SetProfile(string name, string? email, string? phone, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        SetPrimaryValue(ContactType.Email, email, utcNow);
        SetPrimaryValue(ContactType.Phone, phone, utcNow);
        UpdatedAt = utcNow;
    }

    private void SetPrimaryValue(ContactType type, string? value, DateTime utcNow)
    {
        var primary = PrimaryContact(type);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (primary is not null)
            {
                Remove(primary);
            }

            return;
        }

        var normalized = CustomerContact.Normalize(type, value);
        var existing = FindContact(type, normalized);
        if (existing is not null)
        {
            MakePrimary(existing);
        }
        else if (primary is not null)
        {
            primary.ChangeValue(normalized);
            SyncPrimaryValues();
        }
        else
        {
            var contact = CustomerContact.Create(Id, type, normalized, utcNow);
            _contacts.Add(contact);
            MakePrimary(contact);
        }
    }

    private void MakePrimary(CustomerContact contact)
    {
        foreach (var other in _contacts.Where(c => c.Type == contact.Type))
        {
            other.SetPrimary(other == contact);
        }

        SyncPrimaryValues();
    }

    private void Remove(CustomerContact contact)
    {
        _contacts.Remove(contact);
        var next = contact.IsPrimary
            ? _contacts.Where(c => c.Type == contact.Type).OrderBy(c => c.CreatedAt).FirstOrDefault()
            : null;
        if (next is not null)
        {
            MakePrimary(next);
        }
        else
        {
            SyncPrimaryValues();
        }
    }

    private void SyncPrimaryValues()
    {
        Email = PrimaryContact(ContactType.Email)?.Value;
        Phone = PrimaryContact(ContactType.Phone)?.Value;
    }

    private CustomerContact? PrimaryContact(ContactType type) =>
        _contacts.FirstOrDefault(c => c.Type == type && c.IsPrimary);

    private CustomerContact? FindContact(ContactType type, string normalizedValue) =>
        _contacts.FirstOrDefault(c => c.Type == type && c.Value == normalizedValue);

    private CustomerContact GetContact(Guid contactId) =>
        _contacts.FirstOrDefault(c => c.Id == contactId)
        ?? throw new ArgumentException("The customer has no contact with this id.", nameof(contactId));

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted customer cannot be changed.");
        }
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
```

**Create file: `server/src/Crm.Application/Customers/ContactValues.cs`** (write with the editor — regex)

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Crm.Domain.Customers;
using PhoneNumbers;

namespace Crm.Application.Customers;

/// <summary>
/// Converts contact values typed by agents or sent by channels into their stored form, and maps contact types to their
/// API names. Phone numbers become E.164 ("+966501234567") with libphonenumber; numbers without a country code are
/// read as numbers of <see cref="DefaultRegion"/>.
/// </summary>
public static partial class ContactValues
{
    /// <summary>Region of numbers typed without a country code ("0501234567" → "+966501234567").</summary>
    public const string DefaultRegion = "SA";

    /// <summary>Longest phone number input accepted (spaces and brackets included).</summary>
    public const int PhoneInputMaxLength = 32;

    private static readonly PhoneNumberUtil Numbers = PhoneNumberUtil.GetInstance();

    private static readonly Dictionary<string, ContactType> TypesByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["phone"] = ContactType.Phone,
        ["email"] = ContactType.Email,
        ["whatsapp"] = ContactType.WhatsApp,
    };

    /// <summary>
    /// True when <paramref name="input"/> is a real phone number (digits in any script, optional leading "+", spaces,
    /// dashes, dots, brackets; no letters, no extension); <paramref name="e164"/> is then its E.164 form.
    /// </summary>
    public static bool TryNormalizePhone(string? input, [NotNullWhen(true)] out string? e164)
    {
        e164 = null;
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > PhoneInputMaxLength || !PhoneCharacters().IsMatch(value))
        {
            return false;
        }

        try
        {
            var number = Numbers.Parse(value, DefaultRegion);
            if (number.HasExtension || !Numbers.IsValidNumber(number))
            {
                return false;
            }

            e164 = Numbers.Format(number, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    /// <summary>"phone", "email" or "whatsapp" (any case) → the contact type. Numbers ("1") are not accepted.</summary>
    public static bool TryParseType(string? name, out ContactType type)
    {
        type = default;
        return name is not null && TypesByName.TryGetValue(name.Trim(), out type);
    }

    /// <summary>The API name of a contact type: "phone", "email" or "whatsapp".</summary>
    public static string TypeName(ContactType type) => TypesByName.Single(pair => pair.Value == type).Key;

    /// <summary>Optional leading "+", then digits (any script), spaces, dots, dashes and brackets.</summary>
    [GeneratedRegex(@"^\+?[\d\s().\-]+$")]
    private static partial Regex PhoneCharacters();
}
```

**File: `server/src/Crm.Application/Customers/CustomerContracts.cs`** — replace the whole file (`CustomerResponse` gains `Contacts` as its **last** parameter; three new records):

```csharp
namespace Crm.Application.Customers;

/// <summary>GET /api/customers query string: <c>search</c> (name, phone or email), <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record ListCustomersQuery(string? Search, int? Page, int? PageSize);

/// <summary>
/// Body of POST /api/customers and PUT /api/customers/{id}. Only the name is required. Email and phone are the
/// customer's primary email / phone contact (phone in any common format; stored as E.164).
/// </summary>
public sealed record CustomerRequest(string? Name, string? Email, string? Phone);

/// <summary>
/// A customer as the API returns it. <c>Email</c> / <c>Phone</c> are the primary email / phone; <c>Contacts</c> lists
/// every contact (phones first, then emails, then WhatsApp numbers; primary first). <c>CreatedAt</c> / <c>UpdatedAt</c> are UTC.
/// </summary>
public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CustomerContactResponse> Contacts);

/// <summary>One contact: <c>Type</c> is "phone", "email" or "whatsapp"; numbers are E.164, emails lower case.</summary>
public sealed record CustomerContactResponse(Guid Id, string Type, string Value, bool IsPrimary);

/// <summary>
/// Body of POST /api/customers/{id}/contacts. <c>Type</c>: "phone", "email" or "whatsapp". <c>IsPrimary</c> true makes
/// it the primary contact of its type (the first contact of a type is always primary).
/// </summary>
public sealed record CustomerContactRequest(string? Type, string? Value, bool? IsPrimary);

/// <summary>
/// GET /api/customers/lookup query string: exactly one of <c>phone</c> (any common format; matches phone and WhatsApp
/// contacts) or <c>email</c> (case-insensitive). Exact match on the stored value, unlike the list search.
/// </summary>
public sealed record CustomerLookupQuery(string? Phone, string? Email);
```

**File: `server/src/Crm.Application/Customers/CustomerText.cs`** — replace the whole file (`PhoneInvalid` has a new text; seven new properties):

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
        "Enter a valid phone number, e.g. +966501234567 or 0501234567.",
        "أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567.");

    public static string NotFound => LocalizedText.Get(
        "The customer was not found.",
        "العميل غير موجود.");

    public static string ContactTypeField => LocalizedText.Get("Contact type", "نوع جهة الاتصال");

    public static string ContactValueField => LocalizedText.Get("Value", "القيمة");

    public static string ContactTypeInvalid => LocalizedText.Get(
        "Choose phone, email or WhatsApp.",
        "اختر الهاتف أو البريد الإلكتروني أو واتساب.");

    public static string ContactExists => LocalizedText.Get(
        "The customer already has this contact.",
        "جهة الاتصال هذه مسجلة للعميل بالفعل.");

    public static string ContactNotFound => LocalizedText.Get(
        "The contact was not found.",
        "جهة الاتصال غير موجودة.");

    public static string LookupNeedsPhoneOrEmail => LocalizedText.Get(
        "Enter a phone number or an email address.",
        "أدخل رقم هاتف أو بريداً إلكترونياً.");

    public static string LookupPhoneOrEmailOnly => LocalizedText.Get(
        "Look up by phone or by email, not both.",
        "ابحث برقم الهاتف أو بالبريد الإلكتروني، وليس بكليهما.");
}
```

**File: `server/src/Crm.Application/Customers/CustomerRequestValidator.cs`** — replace the whole file (no longer `partial`, no regex):

```csharp
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Create and edit rules: name required; email and phone optional but well-formed when given.</summary>
public sealed class CustomerRequestValidator : AbstractValidator<CustomerRequest>
{
    public CustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Customer.NameMaxLength).WithName(_ => CustomerText.NameField);
        RuleFor(x => x.Email).MaximumLength(Customer.EmailMaxLength).EmailAddress().WithName(_ => CustomerText.EmailField)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        // A real phone number in any common format; the service stores it as E.164.
        RuleFor(x => x.Phone).MaximumLength(Customer.PhoneMaxLength).WithName(_ => CustomerText.PhoneField)
            .Must(phone => ContactValues.TryNormalizePhone(phone, out _)).WithMessage(_ => CustomerText.PhoneInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
```

**Create file: `server/src/Crm.Application/Customers/CustomerContactRequestValidator.cs`**

```csharp
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Add-contact rules: a known type; a real phone number for phone / WhatsApp, a valid address for email.</summary>
public sealed class CustomerContactRequestValidator : AbstractValidator<CustomerContactRequest>
{
    public CustomerContactRequestValidator()
    {
        RuleFor(x => x.Type).Must(type => ContactValues.TryParseType(type, out _))
            .WithName(_ => CustomerText.ContactTypeField).WithMessage(_ => CustomerText.ContactTypeInvalid);

        RuleFor(x => x.Value).NotEmpty().WithName(_ => CustomerText.ContactValueField);
        RuleFor(x => x.Value).MaximumLength(CustomerContact.ValueMaxLength).EmailAddress()
            .WithName(_ => CustomerText.ContactValueField)
            .When(x => IsType(x, ContactType.Email) && !string.IsNullOrWhiteSpace(x.Value));
        RuleFor(x => x.Value).Must(value => ContactValues.TryNormalizePhone(value, out _))
            .WithName(_ => CustomerText.ContactValueField).WithMessage(_ => CustomerText.PhoneInvalid)
            .When(x => (IsType(x, ContactType.Phone) || IsType(x, ContactType.WhatsApp)) && !string.IsNullOrWhiteSpace(x.Value));
    }

    private static bool IsType(CustomerContactRequest request, ContactType type) =>
        ContactValues.TryParseType(request.Type, out var parsed) && parsed == type;
}
```

**Create file: `server/src/Crm.Application/Customers/CustomerLookupQueryValidator.cs`**

```csharp
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Lookup rules: exactly one of phone (a real number) or email (a valid address).</summary>
public sealed class CustomerLookupQueryValidator : AbstractValidator<CustomerLookupQuery>
{
    public CustomerLookupQueryValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().WithMessage(_ => CustomerText.LookupNeedsPhoneOrEmail)
            .When(x => string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).Must(phone => ContactValues.TryNormalizePhone(phone, out _))
            .WithMessage(_ => CustomerText.PhoneInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email).Empty().WithMessage(_ => CustomerText.LookupPhoneOrEmailOnly)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).EmailAddress().WithName(_ => CustomerText.EmailField)
            .When(x => string.IsNullOrWhiteSpace(x.Phone) && !string.IsNullOrWhiteSpace(x.Email));
    }
}
```

(Both validators are registered by the existing `AddValidatorsFromAssembly` in `server/src/Crm.Application/DependencyInjection.cs` line 12 — no DI change.)

**File: `server/src/Crm.Application/Customers/ICustomerRepository.cs`** — replace the whole file:

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
    /// One page of customers whose name or any contact value contains <paramref name="search"/> (case-insensitive,
    /// wildcards taken literally; null = every customer), ordered by name. <c>TotalCount</c> counts every match.
    /// </summary>
    Task<PagedResult<Customer>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>The customer (tracked, so changes are saved by <see cref="SaveChangesAsync"/>), or null.</summary>
    Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Customers that have a contact of one of <paramref name="types"/> whose stored value equals
    /// <paramref name="value"/> (already normalized: E.164 / lower-case email), ordered by name. Not tracked.
    /// </summary>
    Task<IReadOnlyList<Customer>> FindByContactAsync(
        IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken);

    void Add(Customer customer);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

**File: `server/src/Crm.Application/Customers/ICustomerService.cs`** — replace the whole file:

```csharp
using Crm.Application.Common.Paging;

namespace Crm.Application.Customers;

/// <summary>
/// Customer profiles (reads need <c>customers.view</c>, writes <c>customers.manage</c> — enforced by the API).
/// Failures: <c>ValidationException</c> 400, <c>NotFoundException</c> 404 (unknown or deleted customer / unknown contact),
/// <c>ConflictException</c> 409 (duplicate contact).
/// </summary>
public interface ICustomerService
{
    Task<PagedResult<CustomerResponse>> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken);

    Task<CustomerResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete: the customer disappears from lists and lookups; the row (and later its tickets) stays.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adds a phone, email or WhatsApp contact (phones stored as E.164). 409 when the customer already has it.</summary>
    Task<CustomerContactResponse> AddContactAsync(Guid customerId, CustomerContactRequest request, CancellationToken cancellationToken);

    /// <summary>Makes the contact the primary one of its type; the old primary is unset.</summary>
    Task MakeContactPrimaryAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>Removes the contact; when it was primary, the oldest other contact of its type becomes primary.</summary>
    Task RemoveContactAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken);

    /// <summary>
    /// Customers with exactly this phone (phone or WhatsApp contact) or email. Used by GET /api/customers/lookup and by
    /// the email / WhatsApp channels to match an incoming message to its customer. Empty list when nobody matches.
    /// </summary>
    Task<IReadOnlyList<CustomerResponse>> LookupAsync(CustomerLookupQuery query, CancellationToken cancellationToken);
}
```

**File: `server/src/Crm.Application/Customers/CustomerService.cs`** — replace the whole file:

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
    IValidator<CustomerRequest> requestValidator,
    IValidator<CustomerContactRequest> contactValidator,
    IValidator<CustomerLookupQuery> lookupValidator) : ICustomerService
{
    private static readonly ContactType[] NumberTypes = [ContactType.Phone, ContactType.WhatsApp];

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

        var customer = Customer.Create(request.Name!, request.Email, PhoneOrNull(request.Phone), UtcNow());
        customers.Add(customer);
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customer = await FindAsync(id, cancellationToken);

        customer.Update(request.Name!, request.Email, PhoneOrNull(request.Phone), UtcNow());
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, cancellationToken);

        customer.Delete(UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerContactResponse> AddContactAsync(
        Guid customerId, CustomerContactRequest request, CancellationToken cancellationToken)
    {
        await contactValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customer = await FindAsync(customerId, cancellationToken);
        ContactValues.TryParseType(request.Type, out var type);
        var value = type == ContactType.Email ? request.Value! : PhoneOrNull(request.Value)!;
        if (customer.HasContact(type, value))
        {
            throw new ConflictException(CustomerText.ContactExists);
        }

        var contact = customer.AddContact(type, value, request.IsPrimary == true, UtcNow());
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(contact);
    }

    public async Task MakeContactPrimaryAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken)
    {
        var customer = await FindWithContactAsync(customerId, contactId, cancellationToken);

        customer.MakeContactPrimary(contactId, UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveContactAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken)
    {
        var customer = await FindWithContactAsync(customerId, contactId, cancellationToken);

        customer.RemoveContact(contactId, UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerResponse>> LookupAsync(CustomerLookupQuery query, CancellationToken cancellationToken)
    {
        await lookupValidator.ValidateOrThrowAsync(query, cancellationToken);

        var found = string.IsNullOrWhiteSpace(query.Phone)
            ? await customers.FindByContactAsync(
                [ContactType.Email], CustomerContact.Normalize(ContactType.Email, query.Email!), cancellationToken)
            : await customers.FindByContactAsync(NumberTypes, PhoneOrNull(query.Phone)!, cancellationToken);

        return [.. found.Select(ToResponse)];
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>E.164 form of a phone number the validator accepted; null for an empty value.</summary>
    private static string? PhoneOrNull(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null
        : ContactValues.TryNormalizePhone(phone, out var e164) ? e164
        : throw new InvalidOperationException("The phone number was not validated.");

    private async Task<Customer> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await customers.FindAsync(id, cancellationToken) ?? throw new NotFoundException(CustomerText.NotFound);

    private async Task<Customer> FindWithContactAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(customerId, cancellationToken);
        return customer.Contacts.Any(c => c.Id == contactId)
            ? customer
            : throw new NotFoundException(CustomerText.ContactNotFound);
    }

    private static CustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.Phone, customer.CreatedAt, customer.UpdatedAt,
            [.. customer.Contacts
                .OrderBy(c => c.Type).ThenByDescending(c => c.IsPrimary).ThenBy(c => c.CreatedAt).ThenBy(c => c.Value)
                .Select(ToResponse)]);

    private static CustomerContactResponse ToResponse(CustomerContact contact) =>
        new(contact.Id, ContactValues.TypeName(contact.Type), contact.Value, contact.IsPrimary);
}
```

**File: `server/src/Crm.Infrastructure/Customers/CustomerRepository.cs`** — replace the whole file (search extended, `FindByContactAsync` added):

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Customers;

/// <summary>
/// EF Core storage of customers. The "SoftDelete" query filter hides deleted customers from every query here; the
/// contacts are owned by the customer, so they are always loaded with it.
/// </summary>
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
            // A search that is a phone number ("050 123 4567") also finds its stored E.164 form ("+966501234567").
            var phone = ContactValues.TryNormalizePhone(search, out var e164) ? e164 : null;
            customers = customers.Where(c =>
                EF.Functions.Like(c.Name, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Email!, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.Like(c.Phone!, pattern, LikePattern.EscapeCharacter)
                || c.Contacts.Any(x => EF.Functions.Like(x.Value, pattern, LikePattern.EscapeCharacter)
                                       || (phone != null && x.Value == phone)));
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

    public async Task<IReadOnlyList<Customer>> FindByContactAsync(
        IReadOnlyCollection<ContactType> types, string value, CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking()
            .Where(c => c.Contacts.Any(x => types.Contains(x.Type) && x.Value == value))
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public void Add(Customer customer) => db.Customers.Add(customer);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
```

**File: `server/src/Crm.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`** — replace the whole file (adds the owned `Contacts` mapping before the query filter):

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

        // Contacts belong to the customer aggregate: owned entities in their own table, always loaded with the
        // customer and hidden together with it by the soft-delete filter.
        customer.OwnsMany(c => c.Contacts, contact =>
        {
            contact.ToTable("CustomerContacts");
            contact.WithOwner().HasForeignKey(x => x.CustomerId);
            contact.HasKey(x => x.Id);
            contact.Property(x => x.Id).ValueGeneratedNever(); // set by Customer.AddContact
            contact.Property(x => x.Type).HasConversion<string>().HasMaxLength(16); // "Phone", "Email", "WhatsApp"
            contact.Property(x => x.Value).HasMaxLength(CustomerContact.ValueMaxLength).IsRequired();
            contact.HasIndex(x => new { x.Type, x.Value }); // lookup by phone / email
            contact.HasIndex(x => new { x.CustomerId, x.Type, x.Value }).IsUnique(); // no duplicate per customer
        });
        customer.Navigation(c => c.Contacts).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Soft delete: deleted customers disappear from every query; their rows (and later their tickets) stay.
        customer.HasQueryFilter(CrmDbContext.SoftDeleteFilter, c => !c.IsDeleted);
    }
}
```

Run `dotnet build` → 0 warnings, 0 errors. Run `dotnet test tests/Crm.UnitTests` → **Green: 213 passed** (121 existing + 21 `CustomerContactTests` + 31 `ContactValuesTests` + 23 `CustomerContactValidatorTests` + 9 new `CustomerServiceTests` + 1 new `ValidRequest_HasNoErrors` row + 7 new `TextProperty_HasEnglishAndArabicText` rows for the new `CustomerText` properties). (A full `dotnet test` now shows 7 CRM-8 integration tests failing — they still send or expect non-E.164 phones; task 3 updates them.)

### 3 — Integration tests (Red)

**File: `server/tests/Crm.Api.IntegrationTests/Customers/CustomerBodies.cs`** — replace the whole file (`Contacts` on `CustomerBody`, `ContactBody`, `TestPhones` helper):

```csharp
namespace Crm.Api.IntegrationTests.Customers;

/// <summary>JSON shapes of /api/customers responses, as the client sees them.</summary>
public sealed record CustomerBody(
    Guid Id, string Name, string? Email, string? Phone, DateTime CreatedAt, DateTime UpdatedAt, ContactBody[] Contacts);

public sealed record ContactBody(Guid Id, string Type, string Value, bool IsPrimary);

public sealed record CustomerPageBody(CustomerBody[] Items, int Page, int PageSize, int TotalCount);

/// <summary>Test phone numbers.</summary>
public static class TestPhones
{
    /// <summary>A random valid Saudi mobile number in E.164 ("+96650" + 7 digits), so tests never share a number.</summary>
    public static string NewMobile() => $"+96650{Random.Shared.Next(1_000_000, 9_999_999)}";

    /// <summary>The same number as Saudis type it: "0" + the national number ("0501234567").</summary>
    public static string Local(string e164) => "0" + e164["+966".Length..];
}
```

**File: `server/tests/Crm.Api.IntegrationTests/Customers/CustomerManagementTests.cs`** — line 41: `Assert.Equal("+966 50 123 4567", customer.Phone);` → `Assert.Equal("+966501234567", customer.Phone); // stored in E.164 (CRM-9)` (the request on line 34 still sends `"+966 50 123 4567"`).

**File: `server/tests/Crm.Api.IntegrationTests/Customers/CustomerListTests.cs`**

- Line 14: `… and phone "+9665&lt;digits&gt;n"` → `… and phone "+96650&lt;6 digits&gt;n"`.
- Line 20 → `        var phonePrefix = $"+96650{Random.Shared.Next(100_000, 999_999)}"; // + n = a valid Saudi mobile number`

**File: `server/tests/Crm.Api.IntegrationTests/Customers/CustomersAuthorizationTests.cs`**

- After line 20 (`{ "DELETE", $"/api/customers/{Guid.Empty}" },`) add:

```csharp
        { "GET", "/api/customers/lookup?phone=%2B966501234567" },
        { "POST", $"/api/customers/{Guid.Empty}/contacts" },
        { "POST", $"/api/customers/{Guid.Empty}/contacts/{Guid.Empty}/primary" },
        { "DELETE", $"/api/customers/{Guid.Empty}/contacts/{Guid.Empty}" },
```

- Line 86 (`Assert.Equal(5, policies.Count);`) → 

```csharp
        Assert.Equal(read, policies["GET /api/customers/lookup"]);
        Assert.Equal(write, policies["POST /api/customers/{id:guid}/contacts"]);
        Assert.Equal(write, policies["POST /api/customers/{id:guid}/contacts/{contactId:guid}/primary"]);
        Assert.Equal(write, policies["DELETE /api/customers/{id:guid}/contacts/{contactId:guid}"]);
        Assert.Equal(9, policies.Count);
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Customers/CustomerContactsTests.cs`** — AC 1, 2, 3:

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Customers;

/// <summary>CRM-9: several phones, emails and WhatsApp numbers per customer, E.164, one primary per type.</summary>
public class CustomerContactsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private Task<HttpClient> AgentClientAsync() => factory.CreateClientWithRoleAsync(Roles.Agent);

    private static async Task<CustomerBody> CreateCustomerAsync(HttpClient client, string? phone = null, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Nour Trading", email, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!;
    }

    private static Task<HttpResponseMessage> AddContactAsync(
        HttpClient client, Guid customerId, string type, string value, bool? isPrimary = null) =>
        client.PostAsJsonAsync($"/api/customers/{customerId}/contacts", new { type, value, isPrimary });

    private static async Task<CustomerBody> GetCustomerAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<CustomerBody>($"/api/customers/{id}"))!;

    [Fact]
    public async Task AddContact_PhoneInE164_IsSaved_Returns201()
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        var phone = TestPhones.NewMobile();

        var response = await AddContactAsync(agent, customer.Id, "phone", phone);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = (await response.Content.ReadFromJsonAsync<ContactBody>())!;
        Assert.Equal($"/api/customers/{customer.Id}/contacts/{contact.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(("phone", phone, true), (contact.Type, contact.Value, contact.IsPrimary));
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal(contact, Assert.Single(saved.Contacts));
        Assert.Equal(phone, saved.Phone);
    }

    [Theory]
    [InlineData("local")] // "0501234567"
    [InlineData("arabic")] // "٠٥٠١٢٣٤٥٦٧"
    [InlineData("spaced")] // "+966 50 123 4567"
    public async Task AddContact_PhoneTypedDifferently_IsSavedInE164(string format)
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        var phone = TestPhones.NewMobile();
        var typed = format switch
        {
            "local" => TestPhones.Local(phone),
            "arabic" => string.Concat(TestPhones.Local(phone).Select(digit => (char)('٠' + (digit - '0')))),
            _ => $"{phone[..4]} {phone[4..6]} {phone[6..9]} {phone[9..]}",
        };

        var response = await AddContactAsync(agent, customer.Id, "whatsapp", typed);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(phone, (await response.Content.ReadFromJsonAsync<ContactBody>())!.Value);
    }

    [Fact]
    public async Task Customer_KeepsManyPhonesEmailsAndWhatsAppNumbers_WithOnePrimaryPerType()
    {
        var agent = await AgentClientAsync();
        var (phone1, phone2) = (TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, phone1, "info@nour.example");

        Assert.Equal(HttpStatusCode.Created, (await AddContactAsync(agent, customer.Id, "phone", phone2)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AddContactAsync(agent, customer.Id, "email", "Sales@Nour.Example")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AddContactAsync(agent, customer.Id, "whatsapp", phone1)).StatusCode);

        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal(
            [("phone", phone1, true), ("phone", phone2, false), ("email", "info@nour.example", true),
             ("email", "sales@nour.example", false), ("whatsapp", phone1, true)],
            saved.Contacts.Select(c => (c.Type, c.Value, c.IsPrimary)));
        Assert.Equal(phone1, saved.Phone);
        Assert.Equal("info@nour.example", saved.Email);
    }

    [Theory]
    [InlineData("phone", "12345", "value")]
    [InlineData("phone", "call me", "value")]
    [InlineData("whatsapp", "+9665012345678", "value")]
    [InlineData("email", "not-an-email", "value")]
    [InlineData("email", "", "value")]
    [InlineData("fax", "+966501234567", "type")]
    public async Task AddContact_InvalidPhoneOrEmail_Returns400WithTheField(string type, string value, string field)
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);

        var response = await AddContactAsync(agent, customer.Id, type, value);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal([field], problem!.Errors.Keys);
        Assert.Empty((await GetCustomerAsync(agent, customer.Id)).Contacts);
    }

    [Fact]
    public async Task AddContact_InvalidPhone_InArabic_ReturnsTheArabicMessage()
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        agent.DefaultRequestHeaders.Add("Accept-Language", "ar");

        var response = await AddContactAsync(agent, customer.Id, "phone", "12345");

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567."], problem!.Errors["value"]);
    }

    [Fact]
    public async Task AddContact_AsPrimary_UnsetsTheOldPrimary()
    {
        var agent = await AgentClientAsync();
        var (oldPhone, newPhone) = (TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, oldPhone);

        var response = await AddContactAsync(agent, customer.Id, "phone", newPhone, isPrimary: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal([(newPhone, true), (oldPhone, false)], saved.Contacts.Select(c => (c.Value, c.IsPrimary)));
        Assert.Equal(newPhone, saved.Phone);
    }

    [Fact]
    public async Task MakeContactPrimary_UnsetsTheOldPrimary_Returns204()
    {
        var agent = await AgentClientAsync();
        var (oldPhone, newPhone) = (TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, oldPhone, "info@nour.example");
        var added = (await (await AddContactAsync(agent, customer.Id, "phone", newPhone)).Content.ReadFromJsonAsync<ContactBody>())!;
        factory.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await agent.PostAsync($"/api/customers/{customer.Id}/contacts/{added.Id}/primary", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal([newPhone], saved.Contacts.Where(c => c.Type == "phone" && c.IsPrimary).Select(c => c.Value));
        Assert.Equal(newPhone, saved.Phone);
        Assert.Equal("info@nour.example", saved.Email); // other types keep their primary
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, saved.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCustomer_WithANewPhone_ChangesThePrimaryPhone_AndKeepsTheOtherContacts()
    {
        var agent = await AgentClientAsync();
        var (phone, whatsApp, newPhone) = (TestPhones.NewMobile(), TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, phone);
        await AddContactAsync(agent, customer.Id, "whatsapp", whatsApp);

        var response = await agent.PutAsJsonAsync($"/api/customers/{customer.Id}",
            new { name = "Nour Trading", phone = TestPhones.Local(newPhone) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal(newPhone, saved.Phone);
        Assert.Equal([("phone", newPhone, true), ("whatsapp", whatsApp, true)],
            saved.Contacts.Select(c => (c.Type, c.Value, c.IsPrimary)));
    }

    [Fact]
    public async Task AddContact_TheCustomerAlreadyHas_Returns409()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, phone);

        var response = await AddContactAsync(agent, customer.Id, "phone", TestPhones.Local(phone));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("The customer already has this contact.", problem!.Detail);
    }

    [Fact]
    public async Task RemoveContact_ThePrimary_PromotesTheNextOne_Returns204()
    {
        var agent = await AgentClientAsync();
        var (first, second) = (TestPhones.NewMobile(), TestPhones.NewMobile());
        var customer = await CreateCustomerAsync(agent, first);
        await AddContactAsync(agent, customer.Id, "phone", second);
        var primaryId = (await GetCustomerAsync(agent, customer.Id)).Contacts.Single(c => c.IsPrimary).Id;

        var response = await agent.DeleteAsync($"/api/customers/{customer.Id}/contacts/{primaryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await GetCustomerAsync(agent, customer.Id);
        Assert.Equal((second, true), (Assert.Single(saved.Contacts).Value, saved.Contacts[0].IsPrimary));
        Assert.Equal(second, saved.Phone);
    }

    [Fact]
    public async Task ContactEndpoints_UnknownCustomerOrContact_Return404()
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        var unknown = Guid.NewGuid();

        var add = await AddContactAsync(agent, unknown, "phone", TestPhones.NewMobile());
        var primary = await agent.PostAsync($"/api/customers/{customer.Id}/contacts/{unknown}/primary", null);
        var remove = await agent.DeleteAsync($"/api/customers/{customer.Id}/contacts/{unknown}");

        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, primary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        Assert.Equal("The contact was not found.", (await remove.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task AddContact_ToADeletedCustomer_Returns404()
    {
        var agent = await AgentClientAsync();
        var customer = await CreateCustomerAsync(agent);
        await agent.DeleteAsync($"/api/customers/{customer.Id}");

        var response = await AddContactAsync(agent, customer.Id, "phone", TestPhones.NewMobile());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Customers/CustomerLookupTests.cs`** — AC 4 and search by contacts:

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Customers;

/// <summary>CRM-9 AC 4: lookup by phone or email (used by the email / WhatsApp channels), and search by any contact.</summary>
public class CustomerLookupTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private Task<HttpClient> AgentClientAsync() => factory.CreateClientWithRoleAsync(Roles.Agent);

    private static async Task<CustomerBody> CreateCustomerAsync(HttpClient client, string name, string? phone = null, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name, email, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!;
    }

    private static async Task<CustomerBody[]> LookupAsync(HttpClient client, string queryString)
    {
        var response = await client.GetAsync($"/api/customers/lookup?{queryString}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody[]>())!;
    }

    private static string Unique() => $"l{Guid.NewGuid():N}"[..12];

    [Fact]
    public async Task Lookup_ByPhone_ReturnsTheMatchingCustomer()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", phone);
        await CreateCustomerAsync(agent, "Someone else", TestPhones.NewMobile());

        var found = await LookupAsync(agent, $"phone={Uri.EscapeDataString(phone)}");

        var match = Assert.Single(found);
        Assert.Equal(customer.Id, match.Id);
        Assert.Equal("Nour Trading", match.Name);
        Assert.Equal(phone, Assert.Single(match.Contacts).Value);
    }

    [Fact]
    public async Task Lookup_ByPhoneTypedLocally_FindsTheE164Number()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", phone);

        var found = await LookupAsync(agent, $"phone={TestPhones.Local(phone)}");

        Assert.Equal(customer.Id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task Lookup_ByPhone_FindsAWhatsAppNumber_AsWhatsAppSendsIt()
    {
        var agent = await AgentClientAsync();
        var whatsApp = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading");
        var added = await agent.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts", new { type = "whatsapp", value = whatsApp });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        // WhatsApp Cloud API sends the sender as digits without "+" ("966501234567").
        var found = await LookupAsync(agent, $"phone={whatsApp.TrimStart('+')}");

        Assert.Equal(customer.Id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task Lookup_ByASecondaryEmail_IsCaseInsensitive()
    {
        var agent = await AgentClientAsync();
        var tag = Unique();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", email: $"info-{tag}@nour.example");
        var added = await agent.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts",
            new { type = "email", value = $"sales-{tag}@nour.example" });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var found = await LookupAsync(agent, $"email={Uri.EscapeDataString($"SALES-{tag.ToUpperInvariant()}@Nour.Example")}");

        Assert.Equal(customer.Id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task Lookup_ReturnsEveryCustomerSharingTheNumber_OrderedByName()
    {
        var agent = await AgentClientAsync();
        var switchboard = TestPhones.NewMobile();
        var tag = Unique();
        await CreateCustomerAsync(agent, $"{tag} B", switchboard);
        await CreateCustomerAsync(agent, $"{tag} A", switchboard);

        var found = await LookupAsync(agent, $"phone={Uri.EscapeDataString(switchboard)}");

        Assert.Equal([$"{tag} A", $"{tag} B"], found.Select(c => c.Name));
    }

    [Fact]
    public async Task Lookup_DoesNotReturnDeletedCustomers()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", phone);
        await agent.DeleteAsync($"/api/customers/{customer.Id}");

        var found = await LookupAsync(agent, $"phone={Uri.EscapeDataString(phone)}");

        Assert.Empty(found);
    }

    [Fact]
    public async Task Lookup_WithoutMatch_ReturnsAnEmptyList()
    {
        var agent = await AgentClientAsync();

        var found = await LookupAsync(agent, $"email=nobody-{Unique()}@nour.example");

        Assert.Empty(found);
    }

    [Theory]
    [InlineData("", "phone")]
    [InlineData("phone=12345", "phone")]
    [InlineData("email=not-an-email", "email")]
    [InlineData("phone=0501234567&email=info@nour.example", "email")]
    public async Task Lookup_WithInvalidOrMissingPhoneOrEmail_Returns400(string queryString, string field)
    {
        var agent = await AgentClientAsync();

        var response = await agent.GetAsync($"/api/customers/lookup?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal([field], problem!.Errors.Keys);
    }

    [Fact]
    public async Task List_SearchByASecondaryContact_FindsTheCustomer()
    {
        var agent = await AgentClientAsync();
        var tag = Unique();
        var customer = await CreateCustomerAsync(agent, "Nour Trading");
        await agent.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts", new { type = "email", value = $"{tag}@nour.example" });

        var page = await agent.GetFromJsonAsync<CustomerPageBody>($"/api/customers?search={tag}");

        Assert.Equal(customer.Id, Assert.Single(page!.Items).Id);
    }

    [Fact]
    public async Task List_SearchByPhoneTypedLocally_FindsTheE164Number()
    {
        var agent = await AgentClientAsync();
        var phone = TestPhones.NewMobile();
        var customer = await CreateCustomerAsync(agent, "Nour Trading", phone);

        var page = await agent.GetFromJsonAsync<CustomerPageBody>(
            $"/api/customers?search={Uri.EscapeDataString(TestPhones.Local(phone))}");

        Assert.Equal(customer.Id, Assert.Single(page!.Items).Id);
    }
}
```

Run `dotnet test` → **Red**: unit 213 passed; integration **Failed: 39, Passed: 132** (171 total). Every new contact / lookup test fails (404 instead of 201/204/200/400/409; 404 instead of 401/403 for the 8 new authorization rows; `KeyNotFoundException` in `CustomerEndpoints_NeedViewToRead_AndManageToWrite`), except `AddContact_ToADeletedCustomer_Returns404` (the route does not exist yet — it guards the real 404 after task 4) and `List_SearchByPhoneTypedLocally_FindsTheE164Number` (the search was extended in task 2).

### 4 — Api endpoints (Green)

**File: `server/src/Crm.Api/Endpoints/CustomersEndpoints.cs`** — replace the whole file (lookup after the list; three contact endpoints after delete):

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

        // Exact match on a phone (phone or WhatsApp contact) or email; the email / WhatsApp channels use the same
        // ICustomerService.LookupAsync to find the customer of an incoming message.
        group.MapGet("/lookup", async ([AsParameters] CustomerLookupQuery query, ICustomerService customers,
                    CancellationToken cancellationToken) =>
                Results.Ok(await customers.LookupAsync(query, cancellationToken)))
            .WithName("LookupCustomers");

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

        group.MapPost("/{id:guid}/contacts", async (Guid id, CustomerContactRequest request, ICustomerService customers,
                    CancellationToken cancellationToken) =>
            {
                var contact = await customers.AddContactAsync(id, request, cancellationToken);
                return Results.Created($"/api/customers/{id}/contacts/{contact.Id}", contact);
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("AddCustomerContact");

        group.MapPost("/{id:guid}/contacts/{contactId:guid}/primary", async (Guid id, Guid contactId,
                    ICustomerService customers, CancellationToken cancellationToken) =>
            {
                await customers.MakeContactPrimaryAsync(id, contactId, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("MakeCustomerContactPrimary");

        group.MapDelete("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId,
                    ICustomerService customers, CancellationToken cancellationToken) =>
            {
                await customers.RemoveContactAsync(id, contactId, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("RemoveCustomerContact");

        return app;
    }
}
```

`/api/customers/lookup` does not collide with `/{id:guid}` (the `guid` constraint rejects `lookup`).

Run `dotnet build` → **0 warnings, 0 errors**. Run `dotnet test` → **Green: 384 passed** (213 unit, 171 integration: 131 existing + 19 `CustomerContactsTests` + 13 `CustomerLookupTests` + 8 new `CustomersAuthorizationTests` rows). `PermissionPolicyTests` (unchanged) now also call the four new routes.

### 5 — Migration (schema + data)

From `server/`, after task 4 builds:

```bash
dotnet build
dotnet ef migrations add AddCustomerContacts --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations
```

Expected: ends with `Done. To undo this action, use 'ef migrations remove'`; creates `<timestamp>_AddCustomerContacts.cs` + `<timestamp>_AddCustomerContacts.Designer.cs` and updates `CrmDbContextModelSnapshot.cs` (adds the owned `CustomerContact` inside the `Customer` entity). The generated `Up` must be exactly the schema part below (if it touches `Customers` columns or any `AspNet*` table, the model is wrong — run `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api` and fix it).

Then **edit only the generated `<timestamp>_AddCustomerContacts.cs`**: append the data part (the `migrationBuilder.Sql(...)` calls, starting at the comment `// CRM-9 data:`) at the end of `Up`, after the second `CreateIndex`. This is the only hand edit of a migration file; `Down` stays as generated (`DropTable("CustomerContacts")`). The complete `Up`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "CustomerContacts",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
            Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            IsPrimary = table.Column<bool>(type: "bit", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_CustomerContacts", x => x.Id);
            table.ForeignKey(
                name: "FK_CustomerContacts_Customers_CustomerId",
                column: x => x.CustomerId,
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_CustomerContacts_CustomerId_Type_Value",
        table: "CustomerContacts",
        columns: new[] { "CustomerId", "Type", "Value" },
        unique: true);

    migrationBuilder.CreateIndex(
        name: "IX_CustomerContacts_Type_Value",
        table: "CustomerContacts",
        columns: new[] { "Type", "Value" });

    // CRM-9 data: every existing email / phone (CRM-8 columns) becomes the customer's primary contact of that
    // type. Emails are trimmed and lower-cased. Phones are normalized as far as SQL can: spaces, dashes, dots and
    // brackets removed; "00…" → "+…"; "0…" → "+966…" (Saudi); "966…" → "+966…"; anything else stays as entered.
    migrationBuilder.Sql(
        """
        UPDATE Customers
        SET Email = NULLIF(LOWER(LTRIM(RTRIM(Email))), ''),
            Phone = NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(Phone)), ' ', ''), '-', ''), '(', ''), ')', ''), '.', ''), '');
        """);
    migrationBuilder.Sql("UPDATE Customers SET Phone = '+' + SUBSTRING(Phone, 3, 32) WHERE Phone LIKE '00%';");
    migrationBuilder.Sql("UPDATE Customers SET Phone = '+966' + SUBSTRING(Phone, 2, 32) WHERE Phone LIKE '0%' AND LEN(Phone) <= 29;");
    migrationBuilder.Sql("UPDATE Customers SET Phone = '+' + Phone WHERE Phone LIKE '966%' AND LEN(Phone) <= 31;");
    migrationBuilder.Sql(
        """
        INSERT INTO CustomerContacts (Id, CustomerId, Type, Value, IsPrimary, CreatedAt)
        SELECT NEWID(), Id, 'Email', Email, CAST(1 AS bit), CreatedAt FROM Customers WHERE Email IS NOT NULL;
        INSERT INTO CustomerContacts (Id, CustomerId, Type, Value, IsPrimary, CreatedAt)
        SELECT NEWID(), Id, 'Phone', Phone, CAST(1 AS bit), CreatedAt FROM Customers WHERE Phone IS NOT NULL;
        """);
}
```

What the data part does (SQL Server only — tests use `EnsureCreated` on SQLite and never run it): trims + lower-cases `Customers.Email`; removes spaces, dashes, brackets and dots from `Customers.Phone`; `00…` → `+…`, `0…` → `+966…`, `966…` → `+966…` (length guards keep the result within `nvarchar(32)`); anything else stays as entered; then inserts one **primary** contact per non-null email and phone (soft-deleted customers included, `CreatedAt` = the customer's). The `Customers` columns stay equal to the primary contacts.

Test it on a throw-away database (never on `CustomerSupportCrm`; no user-secrets needed — `--connection` overrides the connection string). From `server/`, in Git Bash:

```bash
CS='Server=(localdb)\MSSQLLocalDB;Database=Crm9MigrationCheck;Trusted_Connection=True;TrustServerCertificate=True'
dotnet ef database update AddCustomers --connection "$CS" --project src/Crm.Infrastructure --startup-project src/Crm.Api
sqlcmd -S "(localdb)\MSSQLLocalDB" -d Crm9MigrationCheck -b -Q "INSERT INTO Customers (Id, Name, Email, Phone, CreatedAt, UpdatedAt, IsDeleted, DeletedAt) VALUES (NEWID(), N'Spaced', N' Info@Nour.Example ', N'+966 50 123 4568', '2026-10-01', '2026-10-01', 0, NULL), (NEWID(), N'Local', NULL, N'(050) 123-4569', '2026-10-01', '2026-10-01', 0, NULL), (NEWID(), N'NoPlus', NULL, N'966501234571', '2026-10-01', '2026-10-01', 0, NULL), (NEWID(), N'Short', NULL, N'501234572', '2026-10-01', '2026-10-01', 0, NULL), (NEWID(), N'Deleted', N'gone@x.example', N'0501234573', '2026-10-01', '2026-10-02', 1, '2026-10-02');"
dotnet ef database update --connection "$CS" --project src/Crm.Infrastructure --startup-project src/Crm.Api
sqlcmd -S "(localdb)\MSSQLLocalDB" -d Crm9MigrationCheck -W -Q "SELECT c.Name, c.Email, c.Phone, x.Type, x.Value, x.IsPrimary FROM Customers c LEFT JOIN CustomerContacts x ON x.CustomerId = c.Id ORDER BY c.Name, x.Type"
dotnet ef database update AddCustomers --connection "$CS" --project src/Crm.Infrastructure --startup-project src/Crm.Api
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "ALTER DATABASE Crm9MigrationCheck SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE Crm9MigrationCheck;"
```

Expected after the second `database update`: `Spaced` → `info@nour.example` / `+966501234568` (Email + Phone contacts, both primary); `Local` → `+966501234569`; `NoPlus` → `+966501234571`; `Short` → `501234572` (kept as entered); `Deleted` → `gone@x.example` + `+966501234573` contacts. The `database update AddCustomers` (Down) answers `Reverting migration '<timestamp>_AddCustomerContacts'.` and the last command drops the database. Run `dotnet test` again → 384 passed.

### 6 — How later stories build on this (write nothing here; for later planners)

- **Email / WhatsApp channels (CRM-23..26):** match the sender with `ICustomerService.LookupAsync(new CustomerLookupQuery(Phone: from, Email: null))` (WhatsApp: pass the Cloud API `wa_id` — digits without "+"; `"966…"` is read as a Saudi number, numbers of other countries need a leading `"+"`: prefix `"+"` when the value is all digits) or `new CustomerLookupQuery(null, fromAddress)` (email). It throws `ValidationException` for an unparseable sender — call `ContactValues.TryNormalizePhone` first (or catch it) and treat the sender as unknown. 0 matches → the channel story decides (e.g. create a customer with `ICustomerService.CreateAsync` / `AddContactAsync`); several matches (shared numbers are allowed) → it must pick one deterministically (the list is ordered by name) or ask an agent. Webhooks are anonymous + signature-checked (CRM-7 line 674), so they call the service directly, not `GET /api/customers/lookup`.
- **Phone numbers anywhere else** (ticket requester, SMS): store E.164 via `ContactValues.TryNormalizePhone`; validate with `.Must(v => ContactValues.TryNormalizePhone(v, out _)).WithMessage(_ => CustomerText.PhoneInvalid)`; client `isPhoneNumber` from `client/src/features/customers/customer-form-schema.ts` is only a quick pre-check.
- **CRM-10 (details page):** `GET /api/customers/{id}` already returns `contacts`; `getCustomer` / `useCustomer(id)` exist on the client (query key `['customers', 'detail', id]`, invalidated by the `['customers']` prefix). The details page can host `CustomerContactsTable` + `AddContactForm` directly instead of the dialog.
- **New child collections of `Customer`** that are pure aggregate parts (no rows elsewhere point to them) may follow `OwnsMany` like the contacts; entities that other rows reference or that have their own lifecycle (timeline items, notes, attachments, tickets) stay regular entities with `CustomerId` FK + `Restrict` (CRM-8 line 1576).
- **Contact changes in the timeline (CRM-10):** `Customer.AddContact` / `MakeContactPrimary` / `RemoveContact` are the single places to raise "contact added / changed" events if the timeline wants them.

---

## Frontend Tasks

All commands run from `client/`. **No new npm package.** One new shadcn component (`native-select`). Follow `vercel-react-best-practices` (direct imports, no barrel files).

### 1 — Tests first (Red)

**File: `client/src/api/customers.test.ts`** — replace the whole file (four new tests for `getCustomer`, `addCustomerContact`, `makeCustomerContactPrimary`, `removeCustomerContact`):

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  addCustomerContact,
  createCustomer,
  deleteCustomer,
  getCustomer,
  listCustomers,
  makeCustomerContactPrimary,
  removeCustomerContact,
  updateCustomer,
} from './customers'

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

  it('reads one customer with its contacts with GET /api/customers/{id}', async () => {
    const fetchMock = fakeFetch(200, { id: 'c1', contacts: [] })

    await getCustomer('c1')

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1', method: 'GET', body: undefined })
  })

  it('adds a contact with POST /api/customers/{id}/contacts', async () => {
    const fetchMock = fakeFetch(201, { id: 'k1' })
    const request = { type: 'whatsapp' as const, value: '0501234567', isPrimary: true }

    await addCustomerContact('c1', request)

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1/contacts', method: 'POST', body: request })
  })

  it('makes a contact primary with POST …/contacts/{contactId}/primary and no body', async () => {
    const fetchMock = fakeFetch(204)

    await makeCustomerContactPrimary('c1', 'k1')

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1/contacts/k1/primary', method: 'POST', body: undefined })
  })

  it('removes a contact with DELETE /api/customers/{id}/contacts/{contactId}', async () => {
    const fetchMock = fakeFetch(204)

    await removeCustomerContact('c1', 'k1')

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1/contacts/k1', method: 'DELETE', body: undefined })
  })
})
```

**Create file: `client/src/pages/customers/CustomerContacts.test.tsx`** — AC 1–3 from the user's side (API module mocked, real React Query, real dialog, real toasts):

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import {
  addCustomerContact,
  getCustomer,
  listCustomers,
  makeCustomerContactPrimary,
  removeCustomerContact,
  type Customer,
} from '@/api/customers'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { CustomersPage } from './CustomersPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/customers', () => ({
  listCustomers: vi.fn(),
  getCustomer: vi.fn(),
  createCustomer: vi.fn(),
  updateCustomer: vi.fn(),
  deleteCustomer: vi.fn(),
  addCustomerContact: vi.fn(),
  makeCustomerContactPrimary: vi.fn(),
  removeCustomerContact: vi.fn(),
}))

/** An agent: may view and manage customers. */
const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage, permissions.ticketsView, permissions.ticketsManage],
}
/** A user who may only look at customers: sees the contacts, cannot change them. */
const signedInViewer: CurrentUser = { ...signedInAgent, id: '7', permissions: [permissions.customersView] }

const nour: Customer = {
  id: 'c1',
  name: 'Nour Trading',
  email: 'info@nour.example',
  phone: '+966501234567',
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
  contacts: [
    { id: 'k1', type: 'phone', value: '+966501234567', isPrimary: true },
    { id: 'k2', type: 'phone', value: '+966551234567', isPrimary: false },
    { id: 'k3', type: 'email', value: 'info@nour.example', isPrimary: true },
    { id: 'k4', type: 'whatsapp', value: '+966561234567', isPrimary: true },
  ],
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <CustomersPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

/** Opens the contacts dialog of Nour Trading from the customers table. */
async function openContacts() {
  renderPage()
  const row = await screen.findByRole('row', { name: /Nour Trading/ })
  fireEvent.click(within(row).getByRole('button', { name: 'Contacts' }))
  const dialog = await screen.findByRole('dialog', { name: 'Contacts of Nour Trading' })
  await within(dialog).findByText('+966551234567')
  return dialog
}

function contactRow(dialog: HTMLElement, value: string) {
  return within(dialog).getByRole('row', { name: new RegExp(value.replace('+', '\\+')) })
}

function fillContactForm(dialog: HTMLElement, values: { type?: string; value?: string }) {
  if (values.type !== undefined) fireEvent.change(within(dialog).getByLabelText('Type'), { target: { value: values.type } })
  if (values.value !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Phone number or email'), { target: { value: values.value } })
}

describe('Customer contacts', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(listCustomers).mockReset().mockResolvedValue({ items: [nour], page: 1, pageSize: 20, totalCount: 1 })
    vi.mocked(getCustomer).mockReset().mockResolvedValue(nour)
    vi.mocked(addCustomerContact).mockReset()
    vi.mocked(makeCustomerContactPrimary).mockReset().mockResolvedValue(undefined)
    vi.mocked(removeCustomerContact).mockReset().mockResolvedValue(undefined)
  })

  it('lists every phone, email and WhatsApp number with the primary ones marked', async () => {
    const dialog = await openContacts()

    expect(getCustomer).toHaveBeenCalledWith('c1', expect.anything())
    expect(within(contactRow(dialog, '+966501234567')).getByText('Phone')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966501234567')).getByText('Primary')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966551234567')).queryByText('Primary')).not.toBeInTheDocument()
    expect(within(contactRow(dialog, 'info@nour.example')).getByText('Email')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966561234567')).getByText('WhatsApp')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966561234567')).getByText('Primary')).toBeInTheDocument()
  })

  it('adds a phone number and reloads the contacts', async () => {
    vi.mocked(addCustomerContact).mockResolvedValue({ id: 'k5', type: 'phone', value: '+966571234567', isPrimary: false })
    const dialog = await openContacts()

    fillContactForm(dialog, { value: ' 057 123 4567 ' })
    fireEvent.click(await within(dialog).findByRole('button', { name: 'Add contact' }))

    await waitFor(() =>
      expect(addCustomerContact).toHaveBeenCalledWith('c1', { type: 'phone', value: '057 123 4567', isPrimary: false }),
    )
    expect(await screen.findByText('Contact added.')).toBeInTheDocument()
    await waitFor(() => expect(getCustomer).toHaveBeenCalledTimes(2))
    expect(within(dialog).getByLabelText('Phone number or email')).toHaveValue('')
  })

  it('adds an email as the primary email', async () => {
    vi.mocked(addCustomerContact).mockResolvedValue({ id: 'k5', type: 'email', value: 'sales@nour.example', isPrimary: true })
    const dialog = await openContacts()

    fillContactForm(dialog, { type: 'email', value: 'sales@nour.example' })
    fireEvent.click(await within(dialog).findByRole('checkbox', { name: 'Make it the primary contact of its type' }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add contact' }))

    await waitFor(() =>
      expect(addCustomerContact).toHaveBeenCalledWith('c1', { type: 'email', value: 'sales@nour.example', isPrimary: true }),
    )
  })

  it('checks the value before calling the API', async () => {
    const dialog = await openContacts()
    const add = await within(dialog).findByRole('button', { name: 'Add contact' })

    fireEvent.click(add)
    expect(await within(dialog).findByText('Enter a phone number or an email address.')).toBeInTheDocument()

    fillContactForm(dialog, { value: 'call me' })
    fireEvent.click(add)
    expect(await within(dialog).findByText('Enter a valid phone number, e.g. +966501234567 or 0501234567.')).toBeInTheDocument()

    fillContactForm(dialog, { type: 'email', value: 'not-an-email' })
    fireEvent.click(add)
    expect(await within(dialog).findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Phone number or email')).toHaveAttribute('aria-invalid', 'true')
    expect(addCustomerContact).not.toHaveBeenCalled()
  })

  it('shows the server message next to the value when the API answers 400', async () => {
    vi.mocked(addCustomerContact).mockRejectedValue(
      new ApiError('POST /api/customers/c1/contacts failed with status 400', 400, {
        status: 400,
        errors: { value: ['That number does not exist.'] },
      }),
    )
    const dialog = await openContacts()

    fillContactForm(dialog, { value: '0521234567' })
    fireEvent.click(await within(dialog).findByRole('button', { name: 'Add contact' }))

    expect(await within(dialog).findByText('That number does not exist.')).toBeInTheDocument()
  })

  it('shows the conflict message next to the value when the customer already has the contact', async () => {
    vi.mocked(addCustomerContact).mockRejectedValue(
      new ApiError('POST /api/customers/c1/contacts failed with status 409', 409, {
        status: 409,
        detail: 'The customer already has this contact.',
      }),
    )
    const dialog = await openContacts()

    fillContactForm(dialog, { value: '0501234567' })
    fireEvent.click(await within(dialog).findByRole('button', { name: 'Add contact' }))

    expect(await within(dialog).findByText('The customer already has this contact.')).toBeInTheDocument()
  })

  it('makes another phone the primary one', async () => {
    const dialog = await openContacts()

    fireEvent.click(await within(contactRow(dialog, '+966551234567')).findByRole('button', { name: 'Make primary' }))

    await waitFor(() => expect(makeCustomerContactPrimary).toHaveBeenCalledWith('c1', 'k2'))
    expect(await screen.findByText('Primary contact changed.')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966501234567')).queryByRole('button', { name: 'Make primary' })).not.toBeInTheDocument()
  })

  it('removes a contact', async () => {
    const dialog = await openContacts()

    fireEvent.click(await within(contactRow(dialog, '+966551234567')).findByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(removeCustomerContact).toHaveBeenCalledWith('c1', 'k2'))
    expect(await screen.findByText('Contact removed.')).toBeInTheDocument()
  })

  it('shows the contacts read-only to a user who may only view customers', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInViewer)
    const dialog = await openContacts()
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(within(contactRow(dialog, '+966551234567')).getByText('Phone')).toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Add contact' })).not.toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Make primary' })).not.toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument()
  })
})
```

("Nour Trading", the numbers … are test data in a test file; `no-hardcoded-text.test.ts` skips `*.test.tsx`.)

**File: `client/src/pages/customers/CustomersPage.test.tsx`**

- After line 39 (`updatedAt: '2026-10-01T08:00:00Z',` of `nour`) and after line 47 (`updatedAt: '2026-10-02T08:00:00Z',` of `omar`) add `  contacts: [],`.
- Lines 163–165 → `    expect(within(dialog).getByText('Enter a valid phone number, e.g. +966501234567 or 0501234567.')).toBeInTheDocument()`

**File: `client/src/test/fake-api.ts`** — after line 80 (`updatedAt: '2026-10-01T08:00:00Z',` inside the `/api/customers` branch) add `        contacts: [],`.

Run `npm test` → **Red**: `Test Files 3 failed | 21 passed (24)`, `Tests 14 failed | 489 passed (503)` — the 4 new API tests (`getCustomer is not a function` …), the 9 `CustomerContacts.test.tsx` tests (`Unable to find an accessible element with the role "button" and name "Contacts"`), and `requires a name and checks email and phone before calling the API` (old phone message).

### 2 — shadcn component

```bash
npx shadcn@4.21.2 add native-select -y
```

Creates `client/src/components/ui/native-select.tsx` (`NativeSelect`, `NativeSelectOption`, `NativeSelectOptGroup`; logical classes `pe-8 ps-2.5 end-2.5` because `components.json` has `rtl: true`; no `package.json` change). Do not edit it.

### 3 — API module, schemas, hook

**File: `client/src/api/customers.ts`** — replace the whole file:

```ts
import { apiDelete, apiGet, apiPost, apiPut } from './client'
import { listPath, type ListParams, type PagedResult } from './paging'

/** Kind of a customer contact (server: CustomerContactResponse.type). */
export type ContactType = 'phone' | 'email' | 'whatsapp'

/** One phone number, email address or WhatsApp number. Numbers are E.164 ("+966501234567"), emails lower case. */
export interface CustomerContact {
  id: string
  type: ContactType
  value: string
  isPrimary: boolean
}

/**
 * A customer profile (server: CustomerResponse). `email` / `phone` are the primary email / phone; `contacts` lists
 * every contact (phones, then emails, then WhatsApp numbers; primary first). Times are UTC ISO strings.
 */
export interface Customer {
  id: string
  name: string
  email: string | null
  phone: string | null
  createdAt: string
  updatedAt: string
  contacts: CustomerContact[]
}

/** Body of create and edit. Only the name is required; send null for an empty email or phone. */
export interface CustomerRequest {
  name: string
  email: string | null
  phone: string | null
}

/** Body of "add contact". Phone numbers may be typed in any common format; the server stores E.164. */
export interface CustomerContactRequest {
  type: ContactType
  value: string
  isPrimary: boolean
}

/** GET /api/customers: `search` matches name, phone or email. */
export function listCustomers(params: ListParams, signal?: AbortSignal): Promise<PagedResult<Customer>> {
  return apiGet<PagedResult<Customer>>(listPath('/api/customers', params), signal)
}

/** One customer with all its contacts. */
export function getCustomer(id: string, signal?: AbortSignal): Promise<Customer> {
  return apiGet<Customer>(`/api/customers/${encodeURIComponent(id)}`, signal)
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

/** Adds a contact; the first contact of a type (or one sent with isPrimary) becomes the primary one. */
export function addCustomerContact(customerId: string, request: CustomerContactRequest): Promise<CustomerContact> {
  return apiPost<CustomerContact>(`/api/customers/${encodeURIComponent(customerId)}/contacts`, request)
}

/** Makes the contact the primary one of its type (the old primary is unset). */
export function makeCustomerContactPrimary(customerId: string, contactId: string): Promise<void> {
  return apiPost<void>(
    `/api/customers/${encodeURIComponent(customerId)}/contacts/${encodeURIComponent(contactId)}/primary`,
    undefined,
  )
}

/** Removes the contact; when it was primary, the next contact of its type becomes primary. */
export function removeCustomerContact(customerId: string, contactId: string): Promise<void> {
  return apiDelete(`/api/customers/${encodeURIComponent(customerId)}/contacts/${encodeURIComponent(contactId)}`)
}
```

**File: `client/src/features/customers/customer-form-schema.ts`** — replace the whole file (`isPhoneNumber` accepts any-script digits and dots; it is now only a quick pre-check before the server's real one):

```ts
import type { TFunction } from 'i18next'
import { z } from 'zod'

/**
 * Quick client check before the API's real one (libphonenumber): optional leading +, digits in any script
 * (also ٠–٩), spaces, dots, dashes, brackets; at least 6 digits.
 */
export function isPhoneNumber(phone: string): boolean {
  return /^\+?[\p{Nd}\s().-]+$/u.test(phone) && (phone.match(/\p{Nd}/gu)?.length ?? 0) >= 6
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

**Create file: `client/src/features/customers/contact-form-schema.ts`**

```ts
import type { TFunction } from 'i18next'
import { z } from 'zod'
import type { ContactType } from '@/api/customers'
import { isPhoneNumber } from './customer-form-schema'

/** Contact types in the order the form offers them. */
export const contactTypes = ['phone', 'email', 'whatsapp'] as const satisfies readonly ContactType[]

/** Client-side checks of the "add contact" form (the server validates again and normalizes numbers to E.164). */
export function createContactFormSchema(t: TFunction) {
  return z
    .object({
      type: z.enum(contactTypes),
      value: z.string().trim().min(1, t('customers.contacts.valueRequired')).max(256),
      isPrimary: z.boolean(),
    })
    .superRefine(({ type, value }, context) => {
      const valid = type === 'email' ? z.email().safeParse(value).success : isPhoneNumber(value)
      if (value !== '' && !valid) {
        context.addIssue({
          code: 'custom',
          path: ['value'],
          message: t(type === 'email' ? 'customers.emailInvalid' : 'customers.phoneInvalid'),
        })
      }
    })
}

export type ContactFormValues = z.infer<ReturnType<typeof createContactFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const contactFormFields = ['type', 'value'] as const
```

**File: `client/src/features/customers/useCustomers.ts`** — replace the whole file (adds `useCustomer`):

```ts
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getCustomer, listCustomers } from '@/api/customers'
import type { ListParams } from '@/api/paging'

/** Prefix of every customers query: mutations invalidate it so every page, search and customer reloads. */
export const customersQueryKey = ['customers'] as const

/** One page of GET /api/customers. The previous page stays visible while the next one loads. */
export function useCustomers(params: ListParams) {
  return useQuery({
    queryKey: [...customersQueryKey, params],
    queryFn: ({ signal }) => listCustomers(params, signal),
    placeholderData: keepPreviousData,
  })
}

/** One customer with all its contacts (GET /api/customers/{id}). */
export function useCustomer(id: string) {
  return useQuery({
    queryKey: [...customersQueryKey, 'detail', id],
    queryFn: ({ signal }) => getCustomer(id, signal),
  })
}
```

### 4 — Translations

**File: `client/src/i18n/en.json`**

- Line 135 → `    "phoneInvalid": "Enter a valid phone number, e.g. +966501234567 or 0501234567.",`
- Line 140 (`"deleted": "Customer {{name}} was deleted."`) → (comma added, new keys after it, still inside `customers`):

```json
    "deleted": "Customer {{name}} was deleted.",
    "contactsButton": "Contacts",
    "contacts": {
      "title": "Contacts of {{name}}",
      "description": "Phone numbers, email addresses and WhatsApp numbers. Incoming emails and WhatsApp messages are matched to the customer by them.",
      "columns": {
        "type": "Type",
        "value": "Value",
        "primary": "Primary",
        "actions": "Actions"
      },
      "types": {
        "phone": "Phone",
        "email": "Email",
        "whatsapp": "WhatsApp"
      },
      "primary": "Primary",
      "makePrimary": "Make primary",
      "remove": "Remove",
      "empty": "No contacts yet.",
      "loading": "Loading contacts…",
      "addTitle": "Add a contact",
      "type": "Type",
      "value": "Phone number or email",
      "isPrimary": "Make it the primary contact of its type",
      "add": "Add contact",
      "adding": "Adding…",
      "close": "Close",
      "valueRequired": "Enter a phone number or an email address.",
      "added": "Contact added.",
      "primaryChanged": "Primary contact changed.",
      "removed": "Contact removed."
    }
```

**File: `client/src/i18n/ar.json`** — same places, same keys:

- Line 135 → `    "phoneInvalid": "أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567.",`
- Line 140 → 

```json
    "deleted": "تم حذف العميل {{name}}.",
    "contactsButton": "جهات الاتصال",
    "contacts": {
      "title": "جهات اتصال {{name}}",
      "description": "أرقام الهاتف وعناوين البريد الإلكتروني وأرقام واتساب. تُربط رسائل البريد ورسائل واتساب الواردة بالعميل من خلالها.",
      "columns": {
        "type": "النوع",
        "value": "القيمة",
        "primary": "أساسي",
        "actions": "الإجراءات"
      },
      "types": {
        "phone": "هاتف",
        "email": "بريد إلكتروني",
        "whatsapp": "واتساب"
      },
      "primary": "أساسي",
      "makePrimary": "تعيين كأساسي",
      "remove": "إزالة",
      "empty": "لا توجد جهات اتصال بعد.",
      "loading": "جارٍ تحميل جهات الاتصال…",
      "addTitle": "إضافة جهة اتصال",
      "type": "النوع",
      "value": "رقم الهاتف أو البريد الإلكتروني",
      "isPrimary": "اجعلها جهة الاتصال الأساسية من نوعها",
      "add": "إضافة جهة الاتصال",
      "adding": "جارٍ الإضافة…",
      "close": "إغلاق",
      "valueRequired": "أدخل رقم هاتف أو بريداً إلكترونياً.",
      "added": "تمت إضافة جهة الاتصال.",
      "primaryChanged": "تم تغيير جهة الاتصال الأساسية.",
      "removed": "تمت إزالة جهة الاتصال."
    }
```

`customers.phoneInvalid` is the same text as the server's `CustomerText.PhoneInvalid` on purpose. Do not quote any English value in code or comments (`no-hardcoded-text.test.ts`).

### 5 — Components and page

**Create file: `client/src/features/customers/CustomerContactsTable.tsx`**

```tsx
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { makeCustomerContactPrimary, removeCustomerContact, type CustomerContact } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { customersQueryKey } from './useCustomers'

interface CustomerContactsTableProps {
  customerId: string
  contacts: CustomerContact[]
}

/** The customer's contacts, with make-primary and remove buttons for users who may manage customers. */
export function CustomerContactsTable({ customerId, contacts }: CustomerContactsTableProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: customersQueryKey })

  const makePrimary = useMutation({
    mutationFn: (contactId: string) => makeCustomerContactPrimary(customerId, contactId),
    onSuccess: async () => {
      await refresh()
      toast.success(t('customers.contacts.primaryChanged'))
    },
  })
  const remove = useMutation({
    mutationFn: (contactId: string) => removeCustomerContact(customerId, contactId),
    onSuccess: async () => {
      await refresh()
      toast.success(t('customers.contacts.removed'))
    },
  })
  const busy = makePrimary.isPending || remove.isPending

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('customers.contacts.columns.type')}</TableHead>
          <TableHead>{t('customers.contacts.columns.value')}</TableHead>
          <TableHead>{t('customers.contacts.columns.primary')}</TableHead>
          <Can permission={permissions.customersManage}>
            <TableHead className="text-end">{t('customers.contacts.columns.actions')}</TableHead>
          </Can>
        </TableRow>
      </TableHeader>
      <TableBody>
        {contacts.map((contact) => (
          <TableRow key={contact.id}>
            <TableCell>{t(`customers.contacts.types.${contact.type}`)}</TableCell>
            {/* Numbers and emails read left to right in Arabic too. */}
            <TableCell dir="ltr" className="text-start">
              {contact.value}
            </TableCell>
            <TableCell>
              {contact.isPrimary ? <Badge variant="secondary">{t('customers.contacts.primary')}</Badge> : null}
            </TableCell>
            <Can permission={permissions.customersManage}>
              <TableCell>
                <div className="flex justify-end gap-2">
                  {contact.isPrimary ? null : (
                    <Button variant="outline" size="sm" disabled={busy} onClick={() => makePrimary.mutate(contact.id)}>
                      {t('customers.contacts.makePrimary')}
                    </Button>
                  )}
                  <Button variant="outline" size="sm" disabled={busy} onClick={() => remove.mutate(contact.id)}>
                    {t('customers.contacts.remove')}
                  </Button>
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

**Create file: `client/src/features/customers/AddContactForm.tsx`**

```tsx
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { addCustomerContact } from '@/api/customers'
import { isApiError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { contactFormFields, contactTypes, createContactFormSchema, type ContactFormValues } from './contact-form-schema'
import { customersQueryKey } from './useCustomers'

/** Add-contact form of the contacts dialog: type, value (phone in any format or email), primary checkbox. */
export function AddContactForm({ customerId }: { customerId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createContactFormSchema(t), [t])
  const form = useForm<ContactFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { type: 'phone', value: '', isPrimary: false },
  })

  const add = useMutation({
    mutationFn: (values: ContactFormValues) => addCustomerContact(customerId, values),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: customersQueryKey })
      toast.success(t('customers.contacts.added'))
      form.reset()
    },
  })

  async function onSubmit(values: ContactFormValues) {
    try {
      await add.mutateAsync(values)
    } catch (caught) {
      // 400: the server's field messages; 409: the customer already has this contact. Both are already in the UI
      // language and are shown next to the fields (every failure also shows a toast).
      if (!isApiError(caught)) return
      if (caught.status === 400) {
        for (const field of contactFormFields) {
          const message = caught.problem?.errors?.[field]?.[0]
          if (message) form.setError(field, { message })
        }
      } else if (caught.status === 409 && caught.problem?.detail) {
        form.setError('value', { message: caught.problem.detail })
      }
    }
  }

  return (
    <form noValidate aria-labelledby="add-contact-title" onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <h3 id="add-contact-title" className="font-medium">
          {t('customers.contacts.addTitle')}
        </h3>
        <div className="grid gap-4 sm:grid-cols-[10rem_1fr]">
          <Controller
            name="type"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="contact-type">{t('customers.contacts.type')}</FieldLabel>
                <NativeSelect {...field} id="contact-type" className="w-full" aria-invalid={fieldState.invalid}>
                  {contactTypes.map((type) => (
                    <NativeSelectOption key={type} value={type}>
                      {t(`customers.contacts.types.${type}`)}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
                {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
              </Field>
            )}
          />
          <Controller
            name="value"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="contact-value">{t('customers.contacts.value')}</FieldLabel>
                <Input {...field} id="contact-value" dir="ltr" autoComplete="off" aria-invalid={fieldState.invalid} />
                {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
              </Field>
            )}
          />
        </div>
        <Controller
          name="isPrimary"
          control={form.control}
          render={({ field }) => (
            <Field orientation="horizontal">
              <Checkbox
                id="contact-primary"
                checked={field.value}
                onCheckedChange={(checked) => field.onChange(checked === true)}
              />
              <FieldLabel htmlFor="contact-primary">{t('customers.contacts.isPrimary')}</FieldLabel>
            </Field>
          )}
        />
        <div>
          <Button type="submit" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting ? t('customers.contacts.adding') : t('customers.contacts.add')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}
```

**Create file: `client/src/features/customers/CustomerContactsDialog.tsx`**

```tsx
import { useTranslation } from 'react-i18next'
import type { Customer } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Can } from '@/features/auth/Can'
import { AddContactForm } from './AddContactForm'
import { CustomerContactsTable } from './CustomerContactsTable'
import { useCustomer } from './useCustomers'

interface CustomerContactsDialogProps {
  customer: Customer
  onClose: () => void
}

/**
 * Phones, emails and WhatsApp numbers of one customer. Loads the customer again (GET /api/customers/{id}), so every
 * change made here (they invalidate the customers queries) shows up at once.
 */
export function CustomerContactsDialog({ customer, onClose }: CustomerContactsDialogProps) {
  const { t } = useTranslation()
  const details = useCustomer(customer.id)
  const contacts = details.data?.contacts

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false} className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('customers.contacts.title', { name: customer.name })}</DialogTitle>
          <DialogDescription>{t('customers.contacts.description')}</DialogDescription>
        </DialogHeader>
        {contacts === undefined ? (
          <p className="text-muted-foreground">{t('customers.contacts.loading')}</p>
        ) : contacts.length === 0 ? (
          <p className="text-muted-foreground">{t('customers.contacts.empty')}</p>
        ) : (
          <CustomerContactsTable customerId={customer.id} contacts={contacts} />
        )}
        <Can permission={permissions.customersManage}>
          <AddContactForm customerId={customer.id} />
        </Can>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            {t('customers.contacts.close')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
```

**File: `client/src/features/customers/CustomersTable.tsx`** — replace the whole file (actions column for everyone; Contacts button; Edit / Delete still behind `<Can>`):

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
  onContacts: (customer: Customer) => void
}

export function CustomersTable({ customers, onEdit, onContacts }: CustomersTableProps) {
  const { t } = useTranslation()

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('customers.columns.name')}</TableHead>
          <TableHead>{t('customers.columns.phone')}</TableHead>
          <TableHead>{t('customers.columns.email')}</TableHead>
          <TableHead className="text-end">{t('customers.columns.actions')}</TableHead>
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
            <TableCell>
              <div className="flex justify-end gap-2">
                {/* Everyone who sees customers sees their contacts; changing them needs customers.manage. */}
                <Button variant="outline" size="sm" onClick={() => onContacts(customer)}>
                  {t('customers.contactsButton')}
                </Button>
                <Can permission={permissions.customersManage}>
                  <Button variant="outline" size="sm" onClick={() => onEdit(customer)}>
                    {t('customers.edit')}
                  </Button>
                  <DeleteCustomerAction customer={customer} />
                </Can>
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
```

**File: `client/src/pages/customers/CustomersPage.tsx`** — replace the whole file (`DialogState` gains `contacts`; the contacts dialog):

```tsx
import { PlusIcon, SearchIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { Customer } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Can } from '@/features/auth/Can'
import { CustomerContactsDialog } from '@/features/customers/CustomerContactsDialog'
import { CustomerFormDialog } from '@/features/customers/CustomerFormDialog'
import { CustomersTable } from '@/features/customers/CustomersTable'
import { useCustomers } from '@/features/customers/useCustomers'

const PAGE_SIZE = 20

type DialogState =
  | { mode: 'create' }
  | { mode: 'edit'; customer: Customer }
  | { mode: 'contacts'; customer: Customer }
  | null

/** Customers page: search (name, phone, email), paged table, create / edit dialog, contacts dialog, delete with confirmation. */
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
          onContacts={(customer) => setDialog({ mode: 'contacts', customer })}
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

      {dialog?.mode === 'contacts' ? (
        <CustomerContactsDialog key={dialog.customer.id} customer={dialog.customer} onClose={() => setDialog(null)} />
      ) : dialog ? (
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

Run `npm test` → **Green: 581 passed in 24 files** (490 existing + 4 `customers.test.ts` + 9 `CustomerContacts.test.tsx` + 78 new `translations.test.ts` rows for 26 new keys × 3 checks). Then `npm run build` (OK, chunk-size warning only) and `npm run lint` (exit 0).

---

## Edge Cases & Failure Modes

- **Phone typed in any common format** (`0501234567`, `+966 50 123 4567`, `(050) 123-4567`, `00966…`, `966…`, Arabic-Indic / Persian digits) → stored as `+966501234567` (`ContactValues.TryNormalizePhone`; `TryNormalizePhone_ValidNumber_ReturnsE164` ×10, `AddContact_PhoneTypedDifferently_IsSavedInE164` ×3). Numbers of other countries need their country code (`+44 …`).
- **Invalid phone** — letters, `++`, extension, too short / long, a range that does not exist (`+96652…`) → 400 `errors.value` / `errors.phone` with `CustomerText.PhoneInvalid` (`InvalidPhoneOrEmail_ReportsValue`, `AddContact_InvalidPhoneOrEmail_Returns400WithTheField`). libphonenumber maps letters to keypad digits, so the character pre-check in `ContactValues.PhoneCharacters` is what rejects `call me`-like vanity input.
- **Invalid / too long email, empty value, unknown type** → 400 on `value` / `type` (`CustomerContactRequestValidator`); type numbers (`"1"`) are rejected (`TryParseType_UnknownName_ReturnsFalse`).
- **Same value twice on one customer** (also typed differently: `0501234567` vs `+966501234567`, `INFO@` vs `info@`) → 409 `CustomerText.ContactExists` (`CustomerService.AddContactAsync` → `Customer.HasContact`); the Domain throws `InvalidOperationException` as a second line; the unique index `IX_CustomerContacts_CustomerId_Type_Value` is the third. The same number as phone **and** WhatsApp is allowed (different types).
- **Two customers with the same number / email** → allowed; lookup returns both, ordered by name (`Lookup_ReturnsEveryCustomerSharingTheNumber_OrderedByName`).
- **One primary per type** → `Customer.MakePrimary` unsets the others of that type; first contact of a type is primary; removing the primary promotes the oldest remaining (`RemoveContact_ThePrimary_PromotesTheOldestRemainingOfThatType`); removing the last clears `Customer.Phone` / `Email`. No database constraint (see Decisions) — every write goes through the aggregate.
- **Edit dialog (CRM-8) with an empty phone** → the primary phone contact is removed and the next phone (if any) becomes primary, so the column may show another number after saving (`Update_WithoutPhone_RemovesThePrimaryPhone_AndPromotesTheNext`). A value the customer already has as secondary becomes primary instead of a duplicate.
- **Unknown customer, deleted customer, unknown contact id, non-GUID ids** → 404 (`CustomerText.NotFound` / `CustomerText.ContactNotFound`; route constraint `{contactId:guid}`). Validation runs before the customer is loaded, so `{}` → 400 even for an unknown customer (`SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` relies on "never 401/403").
- **Lookup input** → exactly one of `phone` / `email`; neither → 400 `errors.phone` ("Enter a phone number or an email address."), both → 400 `errors.email`. A `+` in a query string must be URL-encoded (`%2B`) — unencoded it becomes a space; `966…` still parses for Saudi numbers, other countries do not. Deleted customers are hidden by the `SoftDelete` filter (`Lookup_DoesNotReturnDeletedCustomers`).
- **Search** → `LIKE` on every contact value (escaped as in CRM-8) **or** exact E.164 match when the search text is a valid phone number. A number typed with spaces that is not a valid phone (`050 12`) only finds stored values containing that text.
- **Legacy data** → the migration normalizes what SQL can; a CRM-8 phone that could not be normalized (e.g. `501234572`) stays as entered in the column and the contact; lookups will not find it until an agent edits the customer (the edit dialog then requires a valid number). Customers with a 32-character `0…` number keep it (length guard) instead of failing the migration.
- **Half-applied migration** → schema and `Sql(...)` statements run in the migration transaction on SQL Server: either all applied or none.
- **Concurrent changes to the same customer's contacts** → last write wins (no concurrency token, same as CRM-8); the unique index stops two parallel adds of the same value (the second gets a database error → 500). Accepted for this story.
- **Permissions** → reads (`GET …`, lookup) `customers.view`; contact writes `customers.manage`; no token 401, without permission 403 ProblemDetails (`CustomersApi_WithoutToken_Returns401` / `…_Returns403` rows for the 4 new routes). Client hides Add / Make primary / Remove without `customers.manage` (`shows the contacts read-only …`).
- **Client: 400 / 409 on add** → server message under the value field (already in the UI language) plus the CRM-5 toast; 404 (customer deleted meanwhile) → toast only. Make primary / Remove disable the row buttons while a request runs (`busy`).
- **Client: Arabic** → labels, badge, select options in Arabic; values and inputs `dir="ltr"`; the select uses logical padding (`pe-8 ps-2.5`).

---

## Test Plan

1. **Unit (new)** — `server/tests/Crm.UnitTests/Customers/CustomerContactTests.cs` (Domain, no DB): `AddContact_PhoneInE164_IsSaved` (AC 1), `AddContact_StoresManyPhonesEmailsAndWhatsAppNumbers`, `AddContact_FirstOfItsType_BecomesPrimary`, `AddContact_AsPrimary_UnsetsTheOldPrimary` (AC 3), `AddContact_NotAsPrimary_KeepsTheOldPrimary`, `MakeContactPrimary_UnsetsTheOldPrimary_OfThatTypeOnly` (AC 3), `EveryType_HasExactlyOnePrimary_AfterManyChanges` (AC 3), `AddContact_PhoneNotInE164_Throws` ×4, `AddContact_Email_IsTrimmedAndLowerCased`, `AddContact_TheSameValueTwice_Throws`, `RemoveContact_ThePrimary_PromotesTheOldestRemainingOfThatType`, `RemoveContact_TheLastOfItsType_ClearsTheCustomerValue`, `MakePrimaryOrRemove_OfAnUnknownContact_Throws`, `Create_WithEmailAndPhone_AddsThemAsPrimaryContacts`, `Update_WithANewPhone_ChangesThePrimaryPhone_AndKeepsTheOtherContacts`, `Update_WithAnExistingSecondaryPhone_MakesItPrimary`, `Update_WithoutPhone_RemovesThePrimaryPhone_AndPromotesTheNext`, `Contacts_OfADeletedCustomer_CannotBeChanged` — 21 tests.
2. **Unit (new)** — `.../ContactValuesTests.cs`: `TryNormalizePhone_ValidNumber_ReturnsE164` ×10 (AC 1), `TryNormalizePhone_InvalidNumber_ReturnsFalse` ×12 (AC 2), `TryParseType_KnownName_ReturnsTheType` ×4, `TryParseType_UnknownName_ReturnsFalse` ×4, `TypeName_IsTheApiName` — 31 tests.
3. **Unit (new)** — `.../CustomerContactValidatorTests.cs`: `ValidContact_HasNoErrors` ×4, `InvalidPhoneOrEmail_ReportsValue` ×6 (AC 2), `TooLongEmail_ReportsValue`, `UnknownType_ReportsType` ×3, `InvalidContact_InArabic_HasArabicMessages`, `ValidLookup_HasNoErrors` ×3, `InvalidLookup_ReportsTheField` ×5 — 23 tests.
4. **Unit (modified)** — `.../CustomerServiceTests.cs`: constructor + `Update_ChangesTheProfile_AndUpdatedAt` expects E.164; new `Create_ReturnsThePrimaryContacts_WithTheNormalizedPhone`, `AddContact_NormalizesThePhoneToE164_AndSaves` (AC 1), `AddContact_WithInvalidPhone_ThrowsValidationException_AndSavesNothing` (AC 2), `AddContact_ThatTheCustomerAlreadyHas_ThrowsConflict`, `MakeContactPrimary_UnsetsTheOldPrimary` (AC 3), `ContactChanges_OfAnUnknownCustomerOrContact_ThrowNotFound`, `Lookup_ByPhone_SearchesPhoneAndWhatsApp_WithTheE164Number` (AC 4), `Lookup_ByEmail_SearchesEmails_InLowerCase` (AC 4), `Lookup_WithoutPhoneOrEmail_ThrowsValidationException` — 9 new.
5. **Unit (modified)** — `CustomerTests.cs` (E.164 inputs), `CustomerRequestValidatorTests.cs` (Arabic-Indic digits valid, `+9665012345678` invalid, new Arabic message). `LocalizedTextCatalogTests` gets 7 new rows automatically.
6. **Integration (new)** — `server/tests/Crm.Api.IntegrationTests/Customers/CustomerContactsTests.cs`: `AddContact_PhoneInE164_IsSaved_Returns201` (AC 1), `AddContact_PhoneTypedDifferently_IsSavedInE164` ×3 (AC 1), `Customer_KeepsManyPhonesEmailsAndWhatsAppNumbers_WithOnePrimaryPerType`, `AddContact_InvalidPhoneOrEmail_Returns400WithTheField` ×6 (AC 2), `AddContact_InvalidPhone_InArabic_ReturnsTheArabicMessage` (AC 2), `AddContact_AsPrimary_UnsetsTheOldPrimary` (AC 3), `MakeContactPrimary_UnsetsTheOldPrimary_Returns204` (AC 3), `UpdateCustomer_WithANewPhone_ChangesThePrimaryPhone_AndKeepsTheOtherContacts`, `AddContact_TheCustomerAlreadyHas_Returns409`, `RemoveContact_ThePrimary_PromotesTheNextOne_Returns204`, `ContactEndpoints_UnknownCustomerOrContact_Return404`, `AddContact_ToADeletedCustomer_Returns404` — 19 tests.
7. **Integration (new)** — `.../CustomerLookupTests.cs`: `Lookup_ByPhone_ReturnsTheMatchingCustomer` (AC 4), `Lookup_ByPhoneTypedLocally_FindsTheE164Number` (AC 4), `Lookup_ByPhone_FindsAWhatsAppNumber_AsWhatsAppSendsIt` (AC 4), `Lookup_ByASecondaryEmail_IsCaseInsensitive` (AC 4), `Lookup_ReturnsEveryCustomerSharingTheNumber_OrderedByName`, `Lookup_DoesNotReturnDeletedCustomers`, `Lookup_WithoutMatch_ReturnsAnEmptyList`, `Lookup_WithInvalidOrMissingPhoneOrEmail_Returns400` ×4 (AC 2), `List_SearchByASecondaryContact_FindsTheCustomer`, `List_SearchByPhoneTypedLocally_FindsTheE164Number` — 13 tests.
8. **Integration (modified)** — `CustomersAuthorizationTests.cs` (4 new routes × 401/403 = 8 rows, policy map of 9 endpoints), `CustomerManagementTests.cs` (E.164 phone in the response), `CustomerListTests.cs` (valid Saudi test numbers), `CustomerBodies.cs` (`Contacts`, `TestPhones`).
9. **Unchanged, must stay green (backend)** — `PermissionPolicyTests` (`EveryProtectedApiEndpoint_RequiresAKnownPermission`, `SuperAdmin_IsNeverForbidden_OnAnyApiEndpoint` — now include the 4 new routes), `ProtectedEndpointTests`, `SoftDeleteModelTests.EverySoftDeletableEntity_HasTheSoftDeleteQueryFilter`, `LayerDependencyTests`, all CRM-8 customer tests.
10. **Unit (frontend, modified)** — `client/src/api/customers.test.ts`: 4 new tests (`getCustomer`, `addCustomerContact`, `makeCustomerContactPrimary` without body, `removeCustomerContact`).
11. **Component (frontend, new)** — `client/src/pages/customers/CustomerContacts.test.tsx`: `lists every phone, email and WhatsApp number with the primary ones marked`, `adds a phone number and reloads the contacts` (AC 1), `adds an email as the primary email` (AC 3), `checks the value before calling the API` (AC 2), `shows the server message next to the value when the API answers 400` (AC 2), `shows the conflict message next to the value when the customer already has the contact`, `makes another phone the primary one` (AC 3), `removes a contact`, `shows the contacts read-only to a user who may only view customers` — 9 tests.
12. **Modified (frontend)** — `CustomersPage.test.tsx` (fixtures with `contacts`, new phone message), `fake-api.ts` (`contacts: []`).
13. **Guards (unchanged files, more rows)** — `translations.test.ts` (26 new keys), `no-hardcoded-text.test.ts`, `theme.test.ts` (the new `native-select.tsx` uses logical classes), `permissions.test.ts`.
14. **Migration check + manual smoke** — Backend task 5, Verification step 7.

---

## Migration / Rollback

- **Schema:** migration `AddCustomerContacts` creates `CustomerContacts` (`Id uniqueidentifier PK`, `CustomerId uniqueidentifier NOT NULL` FK → `Customers.Id` `ON DELETE CASCADE`, `Type nvarchar(16) NOT NULL`, `Value nvarchar(256) NOT NULL`, `IsPrimary bit NOT NULL`, `CreatedAt datetime2 NOT NULL`), unique index `IX_CustomerContacts_CustomerId_Type_Value`, index `IX_CustomerContacts_Type_Value`. **Data:** normalizes `Customers.Email` / `Customers.Phone` and copies them as primary contacts (Backend task 5). Applied automatically by `dotnet run` in Development (`Database:StartupAction = Migrate`).
- **Rollback (local DB):** stop the API; from `server/`: `dotnet ef database update AddCustomers --project src/Crm.Infrastructure --startup-project src/Crm.Api` (drops `CustomerContacts` **and every secondary contact**; the `Customers` columns keep the primary values, already normalized — that part is not reverted), then `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api` if the migration is not committed yet. CRM-8 code works on the normalized columns.
- **Half-applied state:** on SQL Server the whole `Up` (DDL + `Sql`) runs in one transaction — a failure leaves the database at `AddCustomers` with the original data.
- **Code rollback without DB rollback:** CRM-8 code ignores `CustomerContacts`; contacts added later are invisible to it, and editing a customer with CRM-8 code changes only the columns (contacts drift). Roll back the database too.
- **`AddCustomers` is not edited** (CRM-8 rule); the only hand edit is appending the `Sql(...)` data part to the newly generated `AddCustomerContacts`.

---

## Verification Steps

1. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings.
2. **Backend tests:** in `server/` run `dotnet test` — **384 passed** (213 unit, 171 integration), 0 failed.
3. **Frontend tests:** in `client/` run `npm test` — **581 passed** in 24 files.
4. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed (the "chunks larger than 500 kB" message is a warning).
5. **Frontend lint:** in `client/` run `npm run lint` — exit code 0, no warnings, no errors.
6. **Migration check:** `git status` shows `server/src/Crm.Infrastructure/Persistence/Migrations/<timestamp>_AddCustomerContacts.cs`, `<timestamp>_AddCustomerContacts.Designer.cs` and the modified `CrmDbContextModelSnapshot.cs`; `Up` = the block in Backend task 5; the throw-away database check of task 5 printed the expected rows and was dropped.
7. **Manual smoke** (user-secrets from CRM-2 already set; this applies `AddCustomerContacts` to the developer database `CustomerSupportCrm` — existing customers get their contacts):
   - Terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http`.
   - Terminal 2: `cd client && npm run dev`, open `http://localhost:5173`, sign in as `admin@crm.local`, open **Customers**.
   - **Add customer** "Nour Trading" with phone `050 123 4567` → the table shows `+966501234567`. Enter phone `12345` in the edit dialog → "Enter a valid phone number, e.g. +966501234567 or 0501234567." under Phone.
   - **Contacts** on the row → dialog "Contacts of Nour Trading" lists the phone with a "Primary" badge. Add type **WhatsApp** `٠٥٥١٢٣٤٥٦٧` → toast "Contact added.", row `+966551234567` (Primary). Add **Phone** `0561234567` with "Make it the primary contact of its type" → it becomes Primary, the old phone shows **Make primary**; close the dialog → the table's Phone column shows `+966561234567`.
   - Add **Phone** `0561234567` again → "The customer already has this contact." under the value. Add **Email** `not-an-email` → "Enter a valid email address.".
   - **Make primary** on the old phone → toast "Primary contact changed."; **Remove** it → toast "Contact removed.", the other phone becomes Primary.
   - Lookup: `curl -H "Authorization: Bearer <token>" "http://localhost:5080/api/customers/lookup?phone=0561234567"` (token from `POST /api/auth/login`) → the customer; `?phone=966551234567` → the same customer (WhatsApp number).
   - Click **العربية** → dialog, select options, badge and toasts in Arabic; numbers stay left-to-right.
8. **Regression:** `git status` shows no changes under `.claude/`, `.mcp.json`, `CLAUDE.md`, `client/src/components/ui/` other than the new `native-select.tsx`, `server/src/Crm.Infrastructure/Identity/`, `server/src/Crm.Infrastructure/Persistence/Migrations/*_AddCustomers*.cs`; `client/src/api/client.ts` is still the only `fetch` caller (`git grep -n "fetch(" -- client/src ':!*.test.*' ':!client/src/test'`); `git grep -n "Microsoft.EntityFrameworkCore\|PhoneNumbers" -- server/src/Crm.Domain` returns nothing.

---

## Done Criteria

- [ ] Adding a phone in E.164 (and in local / Arabic-Indic formats, normalized) is saved: `POST /api/customers/{id}/contacts` → 201 (`AddContact_PhoneInE164_IsSaved_Returns201`, `AddContact_PhoneTypedDifferently_IsSavedInE164`, `AddContact_PhoneInE164_IsSaved`; UI: `adds a phone number and reloads the contacts`) — AC 1.
- [ ] An invalid phone or email returns 400 with the field error (`AddContact_InvalidPhoneOrEmail_Returns400WithTheField`, `CreateCustomer_WithInvalidEmailAndPhone_Returns400WithFieldErrors`, `Lookup_WithInvalidOrMissingPhoneOrEmail_Returns400`; UI: `checks the value before calling the API`, `shows the server message …`) — AC 2.
- [ ] One primary contact per type; a new primary unsets the old one (`AddContact_AsPrimary_UnsetsTheOldPrimary`, `MakeContactPrimary_UnsetsTheOldPrimary_Returns204`, `EveryType_HasExactlyOnePrimary_AfterManyChanges`; UI: `makes another phone the primary one`) — AC 3.
- [ ] Lookup by phone or email returns the matching customer (`CustomerLookupTests`, `Lookup_ByPhone_SearchesPhoneAndWhatsApp_WithTheE164Number`); `ICustomerService.LookupAsync` documented for the channels — AC 4.
- [ ] Contact rules in `Crm.Domain` (no package reference), unit-tested without a DB; libphonenumber only in `Crm.Application`.
- [ ] Reads `customers.view`, writes `customers.manage` (`CustomersAuthorizationTests`); `PermissionPolicyTests` green.
- [ ] Migration `AddCustomerContacts` (table + indexes + data copy) checked on a throw-away database; `AddCustomers` untouched.
- [ ] Contacts dialog on the Customers page; every new string in `en.json` and `ar.json`; server texts in `CustomerText`.
- [ ] Only new dependencies: NuGet `libphonenumber-csharp` 9.0.40 and the shadcn `native-select` component; no hand edits in `client/src/components/ui/`.
- [ ] `dotnet build`, `dotnet test` (384), `npm test` (581), `npm run build`, `npm run lint` all pass.
- [ ] Committed on `feature/crm-9-customer-contacts` with message `CRM-9: customer contact details`.
- [ ] `.squad/plans/customer-management/00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 10.**
