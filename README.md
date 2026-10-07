# Customer Support CRM

A bilingual (Arabic / English) customer support platform built for **AZM Squad**.
Customers reach the company through any channel. Every request becomes a ticket.
Agents resolve tickets against SLA targets, supervisors watch the workload, managers
read the reports, and administrators configure the platform.

Core workflow:

```
customer → ticket → AI categorise → auto-assign → reply on any channel
        → SLA monitor → escalate → resolve → CSAT survey → reports
```

**Stack:** ASP.NET Core (.NET 10) · EF Core 10 · SQL Server · SignalR · Hangfire ·
React 19 + Vite + TypeScript · shadcn/ui + Tailwind CSS v4 · xUnit + Vitest

---

## Contents

1. [Where this stands](#where-this-stands)
2. [How it was built](#how-it-was-built)
3. [Features](#features)
4. [Getting started](#getting-started)
5. [Accounts and roles](#accounts-and-roles)
6. [Configuration and secrets](#configuration-and-secrets)
7. [Repository layout](#repository-layout)
8. [Architecture](#architecture)
9. [API overview](#api-overview)
10. [Commands](#commands)
11. [Testing](#testing)
12. [Database and migrations](#database-and-migrations)
13. [Working on a story (squad-kit flow)](#working-on-a-story-squad-kit-flow)
14. [Conventions and non-negotiable rules](#conventions-and-non-negotiable-rules)
15. [Known gaps](#known-gaps)
16. [Troubleshooting](#troubleshooting)

---

## Where this stands

The project is split into **64 user stories** in **3 phases**, across **13 features**.
All 64 are implemented and tested.

| Phase | Stories | Scope | State |
| --- | --- | --- | --- |
| Phase 1 | 25 | Foundation, auth, users and roles, customers, tickets, SLA, Email and WhatsApp | Merged into `main` |
| Phase 2 | 23 | Auto-assignment, notifications, agent dashboard, audit logs, settings, knowledge base, customer portal, reports | Merged into `main` |
| Phase 3 | 15 | AI features, web forms, live chat, SMS, public API, webhooks, ERP, departments, branches, branding, mobile | Merged into `main` |

Phase 3 detail:

| Stories | Feature |
| --- | --- |
| CRM-50 … 54 | AI features |
| CRM-55 … 57 | Web forms, live chat, SMS |
| CRM-58 … 60 | Public API keys, outgoing webhooks, ERP |
| CRM-61 … 64 | Departments, branches, branding, mobile |

What this means for a reader:

- **Finished work is finished.** Every merged story has tests for each acceptance
  criterion, and `main` builds with **0 warnings**.
- **Deviations are written down.** When a story could not meet an acceptance criterion
  exactly, the reason is recorded in that story's plan under `.squad/plans/`.
- **Gaps are named, not hidden.** See [Known gaps](#known-gaps).

### Where the stories come from

The user stories live in **Notion**, in the database **CRM User Stories** on the
**Customer Support CRM** page. Each story has a Story ID (`CRM-n`), a Feature, a
Description, Acceptance Criteria and a Status.

Every commit traces back to a story:

```
CRM-27: automatic ticket assignment
CRM-44: Customer portal CSAT survey and CSAT read model
```

Each story also has an **intake** (`.squad/stories/<feature>/CRM-<n>/intake.md`) and an
**implementation plan** (`.squad/plans/<feature>/NN-story-<slug>-CRM-<n>.md`).

---

## How it was built

The project was built with **Claude Code** and **squad-kit**, using a
plan-once, execute-many approach.

- **One story at a time, tests first.** Every acceptance criterion became at least one
  test before any production code was written (TDD: red → green → refactor).
- **A strong model plans, a cheaper model executes.** Opus coordinated the work: it split
  the stories into groups, reviewed every migration, ran the full test suites and merged.
  Sonnet agents wrote the plans, the tests and the code.
- **Parallel agent groups.** Up to **4 Sonnet agents** worked at the same time, each on
  its own group of stories, in its own **git worktree** and branch, so no agent could
  break another agent's work.
- **Verified before every merge.** Before a group reached `main`, the full backend and
  frontend suites, the client build and the linter all had to pass, and every new
  migration was checked so that it only added to the schema and never removed a
  migration that already existed on `main`.
- **CRM 01 as a reference.** An earlier version of the same CRM (Spec-Kit, 57 specs and
  881 test cases) was used read-only to find edge cases and test ideas faster.

The work was intense enough to **exhaust the daily usage limit 3 times in a single day**.
Each time, the agents were resumed from where they stopped, and no work was lost,
because every group worked in its own worktree and committed after each story.

### Lessons recorded during the build

- **SQLite in tests does not run migrations.** The integration tests use SQLite with
  `EnsureCreated`, which builds the schema from the model. A merge once dropped a
  migration and the tests stayed green. Since then, all migrations are applied to a
  throwaway SQL Server LocalDB database after each phase.
- **EF migrations are the main merge conflict.** The rule is: never delete or regenerate
  a migration that already exists on `main`. Only a branch's own unmerged migrations may
  be regenerated on top.

---

## Features

The 13 features, as numbered in `.squad/plans/`:

| # | Feature | What it covers |
| --- | --- | --- |
| 01 | Foundation | Project skeleton, global error handling (ProblemDetails), JWT login, app layout, Arabic/English with RTL |
| 02 | Security and admin | User management, roles and permissions, audit logs, system configuration |
| 03 | Customer management | Customer profiles, contact details, interaction timeline, notes and attachments |
| 04 | Ticket management | Create and track tickets, categories and priorities, assignment, status workflow, escalation, history, messages |
| 05 | SLA and automation | SLA policies by priority, business hours, warning and breach monitoring, escalation, auto-assignment, notifications |
| 06 | Channels | Email (SMTP/IMAP), WhatsApp Cloud API, web forms, live chat, SMS |
| 07 | Agent dashboard | My tickets, customer info panel, tasks and reminders, quick replies, @mentions |
| 08 | Knowledge base | Articles and categories, FAQs, Arabic-aware search, linking articles to replies |
| 09 | Customer portal | One-time code login, submit and track tickets, help center, CSAT survey |
| 10 | Reports | Ticket, SLA, agent performance and CSAT reports, CSV/Excel export, management dashboard |
| 11 | AI features | Ticket summaries, suggested replies, auto-categorisation, suggested solutions, portal chatbot |
| 12 | Integrations | Public REST API with keys, outgoing webhooks, ERP lookup |
| 13 | Platform | Departments, branches, custom branding, mobile-friendly layout |

### Highlights

**Tickets and SLA**

- Every ticket has a number (`TKT-000012`), a status, a priority, a category and a channel.
- SLA due times are calculated from the priority and the configured **business hours**.
- A Hangfire job checks every ticket: it sends a warning before the deadline and marks the
  breach after it, with an in-app notification and an email.
- First response is recorded once, the first time an agent replies publicly.

**Channels**

- **Email:** agent replies go out by SMTP with a `[TKT-n]` tag in the subject. Incoming
  mail (IMAP) creates a ticket, or is added to the existing ticket when the tag matches.
  Duplicate emails are ignored.
- **WhatsApp:** replies use the WhatsApp Cloud API. Outside the 24-hour window a
  template message is required. The incoming webhook verifies `X-Hub-Signature-256`.
- **Web forms:** a public form that can be embedded in any website through an iframe,
  with captcha, a honeypot field and a per-IP rate limit (`429` with `Retry-After`).
- **Live chat:** real time over SignalR. Visitors use an embeddable widget, agents use the
  chat console. A finished chat is saved as a ticket with the full transcript.
- **SMS:** a Twilio-compatible provider with signature checks, delivery statuses and a
  segment counter (GSM-7 and UCS-2 / Arabic).
- Failed outgoing messages are retried with back-off.

**AI features** (Claude API)

- **Summaries** of long tickets.
- **Suggested replies** in the customer's language, grounded in published knowledge base
  articles. A suggested reply is never sent automatically; the agent reviews it first.
- **Auto-categorisation** of new tickets. Above the confidence threshold (0.8) the
  category and priority are applied; below it they are shown as a suggestion.
- **Suggested solutions:** the three most relevant published articles, with a keyword
  fallback when AI is unavailable.
- **Portal chatbot** that answers only from published articles, cites them, and hands
  over to a human by creating a ticket.
- **Privacy:** emails and phone numbers are masked before anything is sent to the AI,
  customer names are never sent, and prompts are never logged.
- **Without an API key the platform still runs.** AI endpoints return `503` and the UI
  hides the AI actions.

**Customer portal**

- Login by a one-time code sent to the customer's email.
- Customers see only their own tickets and only public replies, never internal notes.
- A resolved ticket can be reopened within 7 days.
- After resolution, a one-time CSAT survey (1–5 stars, link valid for 7 days) feeds the
  satisfaction report.

**Reports**

- Ticket volume by status, category, channel, priority and day.
- SLA compliance and the list of breached tickets.
- Agent performance: handled tickets, first response, resolution, SLA %, CSAT.
- Customer satisfaction: average, distribution, trend, low ratings with comments.
- CSV and real `.xlsx` export. A management dashboard that refreshes every 30 seconds.

---

## Getting started

### Requirements

| Tool | Version |
| --- | --- |
| .NET SDK | 10 |
| Node.js | 20 or newer |
| SQL Server | LocalDB (installed with Visual Studio) or any SQL Server instance |
| EF Core CLI | `dotnet tool install --global dotnet-ef` |

For working on stories you also need [Claude Code](https://claude.com/claude-code) and
squad-kit (`npm install -g squad-kit`).

### 1. Clone

```bash
git clone https://github.com/George-Nabil-Aziz/Azm-Squad-CRM.git
cd Azm-Squad-CRM
```

### 2. Set the two required secrets

The API will not start without a JWT signing key, and the seed needs a password for the
first admin account. Both are kept in **user-secrets**, never in a committed file.

```bash
cd server
dotnet user-secrets --project src/Crm.Api set "Jwt:SigningKey" "<a random string of at least 32 characters>"
dotnet user-secrets --project src/Crm.Api set "Seed:SuperAdminPassword" "<your password>"
```

### 3. Run the API

```bash
cd server
dotnet run --project src/Crm.Api --launch-profile http
```

In Development the API applies all migrations at startup and seeds the roles and the
SuperAdmin account. The database is `CustomerSupportCrm` on `(localdb)\MSSQLLocalDB`.

### 4. Run the client

```bash
cd client
npm install
npm run dev
```

The Vite dev server proxies `/api` and `/hubs` to the API, so there is no CORS setup in
development.

### 5. Open it

| What | Where |
| --- | --- |
| Staff app | http://localhost:5173 |
| Customer portal | http://localhost:5173/portal |
| API | http://localhost:5080 |
| Health check | http://localhost:5080/api/health |

Sign in with `admin@crm.local` and the password you set in step 2.

Check `/api/health` first when something does not work: it tells you whether the API is
up and can reach the database.

### Development demo data

In the Development environment the API fills an empty system with realistic demo data on startup, so the
dashboard and every report show real numbers (`Seed:DemoData` is `true` in `appsettings.Development.json`).
It creates, once:

- 6 agents and 1 supervisor (`sara@crm.com`, `omar@crm.com`, ... , `supervisor@crm.com`, password = `Seed:SuperAdminPassword`),
  3 departments, 2 branches and 5 ticket categories;
- knowledge base: 4 categories, 8 published articles and 6 FAQs (Arabic and English);
- 60 customers (`@demo.crm.com`, Arabic and English names, Saudi and Egyptian cities, E.164 phones) with notes;
- about 300 tickets over the last 60 days on all channels, with replies, internal notes, SLA breaches (about 10 %),
  and CSAT ratings on about 60 % of the resolved ones; plus a few tasks and notifications for the demo agent.

Nothing is ever sent (no email, WhatsApp or SMS is queued). The seeder runs only when the environment is
`Development` and `Seed:DemoData` is true, and only when no customer with an `@demo.crm.com` email exists (that
is the marker), so restarting never adds it twice. Turn it off with `Seed:DemoData=false` (environment variable
`Seed__DemoData=false`). To get fresh demo data, drop the dev database and restart the API.

---

## Accounts and roles

The seed creates four roles and one SuperAdmin user. Further users are created from the
**Users** page.

| Role | Arabic label | Can do |
| --- | --- | --- |
| `SuperAdmin` | مدير النظام | Everything, including SLA policies, system settings and other SuperAdmins |
| `Admin` | مسؤول | Everything except managing SuperAdmins, SLA policies and system settings |
| `Supervisor` | مشرف فريق | Agent permissions, plus assigning tickets, reports and shared quick replies |
| `Agent` | موظف دعم | Customers and tickets, notifications, tasks, chat, knowledge base (read) |
| `Customer` | — | Portal only. A customer token gets `403` on every staff endpoint |

Permissions are defined once in `Crm.Application/Auth/Permissions.cs`, mapped to roles in
`RolePermissions.cs`, and mirrored on the client in `client/src/auth/permissions.ts`.
The client only uses them to hide what a user cannot do; **the API enforces every one of
them**.

---

## Configuration and secrets

Nothing secret is committed. Secrets go in **user-secrets** (development) or
**environment variables** (servers). Channel credentials can also be stored from the
**Settings** page, where they are encrypted with ASP.NET Data Protection and never
returned by the API.

Every integration is optional. **A missing credential never stops the platform from
starting**; the feature is simply shown as not configured.

### Required

| Key | Purpose |
| --- | --- |
| `Jwt:SigningKey` | Signs the access tokens |
| `Seed:SuperAdminPassword` | Password of `admin@crm.local` on first run |
| `ConnectionStrings:Crm` | SQL Server connection (Development default is in `appsettings.Development.json`) |

### Email (SMTP / IMAP)

| Key | Purpose |
| --- | --- |
| `Channels:Email:FromAddress`, `FromName` | Sender of outgoing mail |
| `Channels:Email:Smtp:Host`, `Port`, user name, password | Sending |
| `Channels:Email:Imap:Host`, `UserName`, password | Reading incoming mail |

### WhatsApp Cloud API

| Key | Purpose |
| --- | --- |
| `Channels:WhatsApp:PhoneNumberId` | The business phone number |
| `Channels:WhatsApp:AccessToken` | Meta access token (secret) |
| `Channels:WhatsApp:AppSecret` | Verifies the webhook signature (secret) |
| `Channels:WhatsApp:VerifyToken` | Webhook verification handshake |
| `Channels:WhatsApp:ApiBaseUrl`, `TemplateLanguage` | Optional |

### SMS (Twilio-compatible)

| Key | Purpose |
| --- | --- |
| `Channels:Sms:AccountSid`, `FromNumber` | Account and sender |
| `Channels:Sms:AuthToken` | Secret, also used to verify webhook signatures |
| `Channels:Sms:ApiBaseUrl` | Provider address |
| `Channels:Sms:WebhookBaseUrl` | The public address the provider calls back |

### Web forms and live chat

| Key | Purpose |
| --- | --- |
| `WebForms:CaptchaSecret` | Captcha secret (without it the captcha check is skipped) |
| `WebForms:CaptchaSiteKey`, `CaptchaVerifyUrl` | Captcha widget and verify endpoint (Turnstile by default) |
| `WebForms:RateLimitRequests`, `RateLimitWindowSeconds` | Default 5 requests per 60 seconds per IP |

### AI (Claude API)

| Key | Purpose |
| --- | --- |
| `Ai:ApiKey` | Anthropic API key (secret). Without it, AI features are off |
| `Ai:Model` | Default `claude-haiku-4-5-20251001` |
| `Ai:ConfidenceThreshold` | Default `0.8` for auto-categorisation |
| `Ai:TimeoutSeconds` | Default `30` |

### Integrations (public API, webhooks, ERP)

| Key | Purpose |
| --- | --- |
| `Integrations:Api:RequestsPerMinute` | Rate limit per API key, default 60 |
| `Integrations:Erp:BaseUrl` | ERP address. `HttpErpClient` calls `{BaseUrl}/customers/{id}/orders` and `/invoices` |
| `Integrations:Erp:ApiKey` | ERP key (secret) |
| `Integrations:Erp:TimeoutSeconds` | ERP call timeout |
| `DataProtection:KeysPath` | Where encryption keys are kept, so encrypted secrets survive a restart |

### Customer portal

| Key | Purpose |
| --- | --- |
| `Portal:BaseUrl` | Public address used in survey and portal links |
| `Portal:ReopenWindowDays` | Default 7 |
| `Portal:SurveyValidDays` | Default 7 |

### Example

```bash
cd server
dotnet user-secrets --project src/Crm.Api set "Ai:ApiKey" "<key>"
dotnet user-secrets --project src/Crm.Api set "Channels:Email:Smtp:Host" "smtp.example.com"
dotnet user-secrets --project src/Crm.Api list
```

---

## Repository layout

| Path | Contents |
| --- | --- |
| `server/src/Crm.Api` | Minimal API endpoints, SignalR hubs, middleware, background workers, `Program.cs` |
| `server/src/Crm.Application` | Use cases (one service per feature), DTOs, validation, permissions |
| `server/src/Crm.Domain` | Entities and business rules. No EF Core, no ASP.NET |
| `server/src/Crm.Infrastructure` | EF Core `CrmDbContext`, migrations, Identity, email, WhatsApp, SMS, AI, Hangfire |
| `server/tests/Crm.UnitTests` | Domain and application rules, no database |
| `server/tests/Crm.Api.IntegrationTests` | The real API over HTTP, SQLite in memory |
| `client/src/api` | The single typed API client. Components never call `fetch` |
| `client/src/auth` | Token storage, current user, permissions |
| `client/src/pages/<area>` | Pages, one folder per area |
| `client/src/features/<feature>` | Feature components |
| `client/src/components/ui` | shadcn/ui components |
| `client/src/i18n` | `ar.json` and `en.json`. All user-facing text lives here |
| `.squad/stories` | One intake per story, taken from Notion |
| `.squad/plans` | One implementation plan per story, numbered by feature |
| `CLAUDE.md` | Project rules for Claude Code and for people |

---

## Architecture

### Backend

```
Api  →  Application  →  Domain  ←  Infrastructure
```

- **Domain** holds the entities and rules (status transitions, SLA calculation). It has
  no dependency on EF Core or ASP.NET, so its rules are unit-tested without a database.
- **Application** has one service per feature (`I<Feature>Service`), request and
  response records, and FluentValidation validators. Failures are exceptions:
  `ValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException`.
- **Api** maps each feature in `Endpoints/<Feature>Endpoints.cs` under
  `/api/<resource>`. A global handler turns every exception into an RFC 7807
  **ProblemDetails** response (`400` with field errors for validation).
- **Infrastructure** implements persistence and the outside world.

Cross-cutting decisions:

| Decision | Detail |
| --- | --- |
| Time | Every date is UTC, taken from an injected `TimeProvider`, so tests control time |
| Pagination | `page` (default 1), `pageSize` (default 20, max 100), returns `PagedResult<T>` |
| Soft delete | `IsDeleted` flag with an EF global query filter |
| Channels | Every channel implements `IChannelProvider`; tests use fakes |
| Background work | Hangfire recurring jobs (SLA monitor, task reminders); channel retries and inbox polling run in a hosted worker |
| Real time | SignalR hubs `/hubs/notifications` and `/hubs/chat` |
| Async | `async`/`await` all the way, with `CancellationToken` passed through |

### Frontend

| Concern | Library |
| --- | --- |
| Routing | `react-router` |
| Server state | `@tanstack/react-query` |
| Forms | `react-hook-form` + `zod` |
| UI | shadcn/ui on Tailwind CSS v4 |
| i18n | `react-i18next`, Arabic and English, RTL |
| Toasts | `sonner` |
| Real time | `@microsoft/signalr` |

Colors come only from theme CSS variables, so custom branding can change them at run
time. Layout uses logical classes (`ms-`, `pe-`, `start-`), so the same screens work
left-to-right and right-to-left.

---

## API overview

All staff endpoints need a JWT (`Authorization: Bearer <token>`) and the right
permission. The main groups:

| Route | Purpose |
| --- | --- |
| `/api/auth` | Login, current user (`/me`) |
| `/api/users` | User management |
| `/api/customers` | Customers, contacts, timeline, notes, attachments |
| `/api/tickets` | Tickets, messages, assignment, history, attachments, AI actions |
| `/api/tickets/mine` | The signed-in agent's tickets, sorted by SLA due time |
| `/api/ticket-categories` | Categories |
| `/api/sla-policies` | SLA targets per priority |
| `/api/notifications` | In-app notifications, unread count, mark read |
| `/api/tasks` | Tasks and reminders |
| `/api/quick-replies` | Personal and shared reply templates |
| `/api/kb` | Knowledge base articles, categories, FAQs, search |
| `/api/reports` | Ticket, SLA, agent, CSAT reports, dashboard, exports |
| `/api/audit-logs` | Audit trail |
| `/api/settings` | System configuration |
| `/api/channels` | Channel status, SMS segment counter |
| `/api/chat-sessions` | Live chat for agents |
| `/api/ai/status` | Whether AI is configured |
| `/api/health` | Health check |

Public and webhook endpoints (no staff token):

| Route | Purpose |
| --- | --- |
| `/api/portal/*` | Customer portal: login code, tickets, help center, surveys, chatbot |
| `/api/public/web-forms` | Embedded web form |
| `/api/public/chat` | Live chat for visitors |
| `/api/webhooks/whatsapp` | Incoming WhatsApp messages (signature checked) |
| `/api/webhooks/sms` | Incoming SMS and delivery statuses (signature checked) |

Integrations and platform:

| Route | Purpose |
| --- | --- |
| `/api/v1/tickets`, `/api/v1/customers` | Versioned public API for external systems, scoped API keys (`X-Api-Key`), per-key rate limit |
| `/swagger` | Swagger UI for the public API (`/openapi/public-v1.json`) |
| `/api/api-keys` | Create and revoke API keys (shown once, stored hashed) |
| `/api/webhooks` | Outgoing webhooks (`ticket.created`, `ticket.resolved`), HMAC-signed, retried with back-off |
| `/api/customers/{id}/erp` | Read-only ERP orders and invoices for a linked customer |
| `/api/departments`, `/api/branches` | Departments (with per-department SLA) and branches; agents only see their own data |
| `/api/branding` | Logo and brand colours, applied to the app and to emails |

---

## Commands

### Backend (from `server/`)

| Command | What it does |
| --- | --- |
| `dotnet build` | Builds the solution. Must show 0 warnings |
| `dotnet test` | Runs unit and integration tests |
| `dotnet test --filter "FullyQualifiedName~Tickets"` | Runs a subset |
| `dotnet run --project src/Crm.Api --launch-profile http` | Starts the API on port 5080 |
| `dotnet ef migrations add <Name> --project src/Crm.Infrastructure --startup-project src/Crm.Api` | Adds a migration |
| `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api` | Checks the model and migrations agree |

### Frontend (from `client/`)

| Command | What it does |
| --- | --- |
| `npm run dev` | Vite dev server on port 5173 |
| `npm test` | Vitest, all tests once |
| `npx vitest run src/pages/tickets` | Runs a subset |
| `npm run build` | Type-check and production build |
| `npm run lint` | ESLint |

### Before every merge

```bash
cd server && dotnet build && dotnet test
cd ../client && npm test && npm run build && npm run lint
```

All five must pass.

---

## Testing

Tests are written **before** the code, one or more per acceptance criterion.

| Suite | Tests on `main` | What it checks |
| --- | --- | --- |
| `Crm.UnitTests` | 1,436 | Domain rules, application services, validators, SLA maths, segment counting |
| `Crm.Api.IntegrationTests` | 682 | The real API over HTTP: status codes, permissions, ProblemDetails, SignalR |
| Client (Vitest + Testing Library) | 3,148 | Screens as a user uses them, by role and label, in both languages |
| **Total** | **5,266** | |

How the integration tests work:

- One shared `CrmApiFactory` (`WebApplicationFactory<Program>`) in the `Testing`
  environment.
- SQLite shared-cache in memory: one connection keeps it alive, each `DbContext` opens its own; schema created
  with `EnsureCreated`.
- Hangfire is not started. Recurring jobs are plain classes that tests call directly.
- A fake `TimeProvider` controls time; fake channel and AI providers replace the real
  ones.

Some client tests guard the project rules automatically: no hard-coded user-facing text,
no hard-coded hex colours, and no direction-specific Tailwind classes.

---

## Database and migrations

- Migrations live in `server/src/Crm.Infrastructure/Persistence/Migrations`.
- In Development they are applied automatically at startup.
- The integration tests do **not** run migrations (they use `EnsureCreated`), so after
  each phase all migrations are applied to a throwaway SQL Server database to prove they
  work on the real engine.

Rules for migrations:

1. Create them only with `dotnet ef migrations add`.
2. **Never delete, rename or regenerate a migration that is already on `main`.**
3. If two branches change the model, the second one to merge regenerates only its own
   unmerged migrations on top of `main`.
4. After adding a migration, `has-pending-model-changes` must report no changes.
5. Review the `Up` method before merging: it should add, not remove, unless the removal
   is intended and explained in the plan.

---

## Working on a story (squad-kit flow)

Every change follows the same flow. Details are in [CLAUDE.md](CLAUDE.md).

```
Notion story → intake.md → implementation plan → tests (red) → code (green) → refactor → merge
```

1. **Read the story** in Notion (Story ID, description, acceptance criteria).
2. **Create the intake:**
   ```bash
   squad new-story 04-ticket-management --id CRM-<n> --title "<title>" --no-fetch -y
   ```
   Copy the title, description and acceptance criteria into `intake.md` verbatim.
3. **Plan** with `/squad-plan <intake path>` in Claude Code. The plan lists the test
   tasks before the implementation tasks. Planning changes no source code.
4. **Implement** in a new session with only the plan attached: tests first, then code.
5. **Commit** as `CRM-<n>: <short summary>`, one story per branch
   (`feature/crm-<n>-<slug>`).

### Team setup for Claude Code

- **Notion:** connect it from claude.ai → Settings → Connectors → Notion, and ask George
  for access to the **Customer Support CRM** page. It is linked to each person's account,
  so it is not in `.mcp.json`.
- **Project tools** load from the repository; approve them the first time:

| Tool | Where |
| --- | --- |
| squad-kit commands `/squad-plan`, `/squad-new-story` | `.claude/commands/` |
| Vercel React best practices skill | `.claude/skills/` |
| .NET skills (dotnet, aspnetcore, data, test) | `.claude/settings.json` |
| shadcn MCP server | `.mcp.json` |

---

## Conventions and non-negotiable rules

1. **Internal notes never reach a customer.** They are filtered in the API, not only
   hidden in the UI.
2. **The server is the security boundary.** Every permission is enforced by an API
   policy. The client only hides what a user cannot do.
3. **No secrets in git.** User-secrets or environment variables only.
4. **Tests before code.** A story is done only when `dotnet test` and `npm test` pass.
5. **Arabic and English from day one.** No hard-coded text; RTL-safe classes only.
6. **One UI library.** shadcn/ui only; colours only through theme variables.
7. **One API client.** All calls go through `client/src/api`.
8. **Dates in UTC** from `TimeProvider`, never `DateTime.Now`.
9. **Missing credentials never crash startup.**

---

## Known gaps

These are known and recorded, not forgotten:

| Gap | Detail |
| --- | --- |
| Migration test | No automated test applies the migrations to SQL Server; it is done by hand after each phase |
| Chatbot rate limit | The anonymous portal chatbot limits message count and size, but has no per-IP rate limit |
| Abandoned chats | A live chat does not close itself; an agent ends it |
| Webhook targets | Outgoing webhook URLs are not checked against private addresses (SSRF); admins are trusted |
| Real credentials | Email, WhatsApp, SMS, AI, captcha and ERP need real credentials to be tested end to end |

---

## Troubleshooting

**The API stops at startup with an error about the port.**
Port 5080 is already in use by another program. Stop it, or run on another port:
`dotnet run --project src/Crm.Api --urls http://localhost:5090`.

**The API stops with a message about `Jwt:SigningKey`.**
The signing key is missing. Set it with `dotnet user-secrets` (see
[Getting started](#getting-started)).

**I cannot sign in as `admin@crm.local`.**
`Seed:SuperAdminPassword` was not set when the database was first created. Set it, then
drop the development database (`CustomerSupportCrm`) and start the API again.

**`Cannot open database` or LocalDB errors.**
Check that LocalDB is running: `sqllocaldb info` and `sqllocaldb start MSSQLLocalDB`.

**The client shows network errors.**
The API is not running, or it is not on port 5080. The Vite proxy expects it there.

**A feature says it is not configured.**
That channel or AI key is missing. See
[Configuration and secrets](#configuration-and-secrets).

**`dotnet ef` is not found.**
Install it: `dotnet tool install --global dotnet-ef`.

**A test passes alone but fails in the full run.**
Check free disk space first: uploaded test files are buffered to the system temp folder, and large-upload tests fail when the disk is full.
