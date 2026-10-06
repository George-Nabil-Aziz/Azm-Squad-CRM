# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/01-foundation/CRM-1/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Foundation
- **Feature slug (folder under `plans/`):** `01-foundation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-1` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Project skeleton (.NET API + React)
```

---

## Description

```
As a developer, I want a solution skeleton (ASP.NET Core Web API + React/Vite/TypeScript) with test projects, so that every feature is built on one consistent structure.
```

---

## Acceptance criteria

```
1. GET /api/health returns 200 with {"status":"ok"}.
2. `dotnet test` runs an xUnit project with at least one passing test.
3. `npm test` in client runs Vitest with at least one passing test.
4. The React dev server proxies /api to the API: the home page calls /api/health and shows "ok".
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** None. This is the first story; the repository has no application code yet.
- **Depends on code areas or other stories:** None. Every later story (CRM-2 onwards) builds on the structure created here.

## Extra notes (optional)

- The repo already contains: `CLAUDE.md` (project rules — read it), `README.md`, `.gitignore`, `.squad/`, `.claude/`, `.mcp.json`. Do not remove or restructure them.
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass.

## Technical hints (optional)

- Installed toolchain: .NET SDK 10.0.400, Node 24, npm.
- Target layout (from `CLAUDE.md`):
  ```
  server/   ASP.NET Core solution (src/ + tests/)
  client/   React app (Vite + TypeScript)
  ```
- Backend layering from `CLAUDE.md`: `Api` → `Application` → `Domain` ← `Infrastructure`. Create the four projects now (empty except what the health check needs) with the correct project references, so later stories only add code. Suggested names: `Crm.Api`, `Crm.Application`, `Crm.Domain`, `Crm.Infrastructure` under `server/src/`.
- Backend tests under `server/tests/`: `Crm.UnitTests` (xUnit) and `Crm.Api.IntegrationTests` (xUnit + `Microsoft.AspNetCore.Mvc.Testing` / `WebApplicationFactory`) — AC 1 is verified by an integration test.
- One solution file in `server/` so `dotnet test` from `server/` runs all test projects.
- Frontend: Vite React + TypeScript template in `client/`, Vitest + React Testing Library + jsdom, `npm test` runs once (not watch mode). AC 4 is verified by a component test that mocks the API client.
- Vite dev server proxy: `/api` → the API's local HTTP URL (from `launchSettings.json`).
- Per `CLAUDE.md`: API calls go through a typed client in `client/src/api`, components never call `fetch` directly.

## Out of scope

- Authentication / JWT (CRM-2).
- App layout, Tailwind and shadcn/ui setup (CRM-3).
- Arabic / English and RTL (CRM-4); the home page text in this story may be a temporary placeholder.
- Global error handling and ProblemDetails (CRM-5).
- Database, EF Core, SQL Server, migrations.
- Docker, CI pipelines, deployment.
