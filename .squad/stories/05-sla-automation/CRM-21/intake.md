# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/05-sla-automation/CRM-21/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** SLA automation
- **Feature slug (folder under `plans/`):** `05-sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-21` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
SLA breach detection (Hangfire job)
```

---

## Description

```
As a supervisor, I want the system to detect SLA breaches automatically, so that late tickets never go unnoticed.
```

---

## Acceptance criteria

```
1. A High ticket with no reply after its response time is marked ResponseBreached.
2. A ticket replied to within its response time is not marked breached.
3. A ticket resolved after ResolutionDueAt is marked ResolutionBreached.
4. Running the job twice does not create duplicate breach records (idempotent).
5. The job runs every minute via Hangfire.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-19 (policy), CRM-20 (due times on tickets), CRM-13 / CRM-14 (tickets).
- **Depends on code areas or other stories:** `Ticket.ResponseDueAt` / `ResolutionDueAt` / `FirstResponseAt` / `ResolvedAt` (CRM-20), `TimeProvider`. CRM-15 (replies) sets `FirstResponseAt`, CRM-17 (status workflow) sets `ResolvedAt` — built later by another group.

## Extra notes (optional)

- CLAUDE.md: Hangfire is not started in `Testing`; recurring jobs are plain classes whose method tests call directly; a fake `TimeProvider` controls time. Hangfire storage = SQL Server; missing configuration must not crash startup.
- Tests first (TDD).

## Technical hints (optional)

- Breach flags on the ticket + one breach record per (ticket, kind), unique — makes the job idempotent.

## Out of scope

- Warnings and escalation (CRM-22), notifications UI, Hangfire dashboard security beyond Development.
