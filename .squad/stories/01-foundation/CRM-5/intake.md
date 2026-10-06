# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/01-foundation/CRM-5/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Foundation
- **Feature slug (folder under `plans/`):** `01-foundation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-5` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Global error handling & validation
```

---

## Description

```
As a developer and user, I want consistent error responses and messages, so that failures are clear and safe.
```

---

## Acceptance criteria

```
1. An invalid request returns 400 ProblemDetails with field-level errors.
2. An unhandled exception returns 500 ProblemDetails with no stack trace in Production.
3. The client shows a toast message when an API call fails.
4. Errors are logged with a correlation id.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1 (project skeleton) — done and merged to `main`.
- **Depends on code areas or other stories:** Builds on `server/` (`Crm.Api`, `Crm.Application`, test projects) and `client/` (`client/src/api/client.ts`) from CRM-1. Runs **before** CRM-2 (JWT auth), CRM-3 (layout + shadcn/ui), CRM-4 (ar/en + RTL); every later story relies on the error contract created here.

## Extra notes (optional)

- Phase 1 execution order: CRM-5, CRM-2, CRM-3, CRM-4, CRM-6, CRM-7, then customers, tickets, SLA, email/WhatsApp.
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass.

## Technical hints (optional)

- `CLAUDE.md` "Architecture decisions": validation with **FluentValidation**; failures expressed as exceptions from `Crm.Application/Common/Exceptions` (`ValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException`) mapped to ProblemDetails by the global handler (this story).
- `CLAUDE.md` "Integration tests": one shared `CrmApiFactory : WebApplicationFactory<Program>` in `Crm.Api.IntegrationTests/Infrastructure/`, environment `Testing` (this story creates it; CRM-2 adds SQLite/JWT config to it).
- `CLAUDE.md` frontend: toasts via shadcn `sonner`; API calls only through `client/src/api`.
- No feature endpoint with input exists yet, so AC 1/2 are exercised through test-only endpoints registered by the integration test host.

## Out of scope

- Authentication / 401 handling and JWT (CRM-2).
- Tailwind, shadcn/ui init and app layout (CRM-3) — this story uses the `sonner` package directly; CRM-3 swaps in the shadcn wrapper.
- i18n / Arabic / RTL of error messages (CRM-4) — toast text is temporary English in one module.
- React Query / form-level inline field errors (later feature stories).
- Centralized log storage (Seq, Application Insights), OpenTelemetry.
