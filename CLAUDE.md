# Customer Support CRM

Customer support CRM for AZM Squad: customers, tickets, SLA, multi-channel (Email, WhatsApp, ...), knowledge base, portal, reports. Arabic + English.

## Stack

- **Backend:** ASP.NET Core Web API (.NET 10), EF Core, SQL Server, SignalR, Hangfire, ASP.NET Identity + JWT
- **Frontend:** React + Vite + TypeScript, shadcn/ui + Tailwind CSS, react-i18next (RTL)
- **Tests:** xUnit (backend), Vitest + React Testing Library (frontend)

Folder layout (once CRM-1 is implemented):

```
server/   ASP.NET Core solution (src/ + tests/)
client/   React app
.squad/   squad-kit stories and plans
```

## Workflow: always squad-kit

Every change goes through the squad-kit flow. No feature work outside it.

1. **Story source = Notion.** Database "CRM User Stories", data source `collection://177d0836-2afb-41c4-9859-f817e905bc2a`. Each story has Story ID (CRM-n), Feature (= squad feature slug), Description, Acceptance Criteria, Status.
2. **Intake:** `squad new-story <feature> --title "<title>"`, then fill `intake.md` from the Notion story (title, description, acceptance criteria verbatim, Story ID in the tracker id field).
3. **Plan:** `/squad-plan <intake-path>`. Planning only, no source changes in that session.
4. **Implement:** new session with only the plan file attached.
5. **Update Notion Status:** Ready → Planned (plan written) → In Progress → Done, and fill "Squad Plan" with the plan path.

**Notion safety:** never delete, move, or edit any Notion page outside the "Customer Support CRM" page. Inside it, only change story properties.

## TDD: tests before code

- Every Acceptance Criterion becomes at least one test.
- Order: write the test → run it and see it fail (Red) → minimum code to pass (Green) → refactor.
- Plans list test tasks **before** implementation tasks.
- A story is not done until `dotnet test` and `npm test` both pass.

## Backend rules (.NET)

- Layers: `Api` (controllers/endpoints) → `Application` (use cases, DTOs, validation) → `Domain` (entities, rules) ← `Infrastructure` (EF Core, email, WhatsApp, Hangfire).
- Domain has no dependency on EF Core or ASP.NET.
- Business rules (SLA calculation, status transitions) live in Domain/Application and are unit tested without a database.
- Errors: RFC 7807 ProblemDetails. Validation errors → 400 with field errors.
- Authorization on the API (policies/permissions), never only in the UI.
- All dates stored in UTC (`DateTime.UtcNow` via an injected clock, `TimeProvider`, so tests can control time).
- Channels (Email, WhatsApp, SMS, ...) implement `IChannelProvider`.
- Secrets never in `appsettings.json` committed to git; use user-secrets / environment variables.
- Async all the way (`async`/`await`, `CancellationToken` passed through).

## Frontend rules (React)

- Follow the `vercel-react-best-practices` skill.
- UI components from shadcn/ui (`client/src/components/ui`); do not add another UI library.
- Colors only through theme CSS variables (needed for custom branding). No hard-coded hex colors in components.
- No hard-coded user-facing text: all strings in i18n files (`ar` and `en`).
- RTL-safe styles: logical Tailwind classes (`ms-`, `me-`, `ps-`, `pe-`, `start-`, `end-`), never `ml-`/`mr-`/`left-`/`right-`.
- API calls go through one typed client in `client/src/api`; components do not call `fetch` directly.
- Test user behavior (React Testing Library queries by role/label), not implementation details.

## Architecture decisions (apply to every story)

Backend:
- **Persistence:** `CrmDbContext` in `Crm.Infrastructure/Persistence/`, derives from `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`. Identity types (`ApplicationUser`, `ApplicationRole`) live in Infrastructure, never in Domain. EF Core migrations live in `Crm.Infrastructure/Persistence/Migrations`.
- **Database:** SQL Server LocalDB in Development, connection string `ConnectionStrings:Crm` = `Server=(localdb)\MSSQLLocalDB;Database=CustomerSupportCrm;Trusted_Connection=True;TrustServerCertificate=True` in `appsettings.Development.json`. Migrations are applied at startup in Development only.
- **Integration tests:** one shared `CrmApiFactory : WebApplicationFactory<Program>` in `Crm.Api.IntegrationTests/Infrastructure/`, environment `Testing`, SQLite in-memory (one open connection kept for the factory lifetime, `EnsureCreated`), test config values (JWT key, seed admin password) injected via `ConfigureAppConfiguration`. Hangfire is not started in `Testing`; recurring jobs are plain classes whose method tests call directly. A fake `TimeProvider` is registered in tests that need time control.
- **Secrets:** `Jwt:SigningKey`, `Seed:SuperAdminPassword`, channel credentials → `dotnet user-secrets` (Api project) or environment variables. Never in committed appsettings.
- **Seed:** roles `SuperAdmin`, `Admin`, `Supervisor`, `Agent` and one SuperAdmin user `admin@crm.local` (password from `Seed:SuperAdminPassword`).
- **Endpoints:** minimal APIs, one static class per feature `Crm.Api/Endpoints/<Feature>Endpoints.cs` with `Map<Feature>Endpoints()`, route groups under `/api/<resource>` (plural, kebab-case).
- **Application layer:** one service per feature (`I<Feature>Service` + implementation) with request/response DTO records; validation with FluentValidation; failures expressed as exceptions from `Crm.Application/Common/Exceptions` (`ValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException`) mapped to ProblemDetails by the global handler (CRM-5).
- **Pagination:** query `page` (default 1) and `pageSize` (default 20, max 100) → `PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`.
- **Soft delete:** `IsDeleted` flag + EF global query filter.
- **Channels:** every channel implements `IChannelProvider`. Real providers (SMTP/IMAP via MailKit, WhatsApp Cloud API via `HttpClient`) are configured from settings; tests use fakes. Missing credentials must not crash startup.

Frontend:
- Routing: `react-router`. Server state: `@tanstack/react-query`. Forms: `react-hook-form` + `zod`. i18n: `react-i18next` (`client/src/i18n/{ar,en}.json`). Toasts: shadcn `sonner`.
- Auth token kept by `client/src/auth/` and attached by `client/src/api/client.ts`; components never read the token directly.
- Pages in `client/src/pages/<area>/`, feature components in `client/src/features/<feature>/`.

## Conventions

- Commit messages: `CRM-<n>: <short summary>` (Notion Story ID).
- One story per branch: `feature/crm-<n>-<slug>`.
- Do not commit `STUDY-NOTES.md` (personal file, git-ignored).
