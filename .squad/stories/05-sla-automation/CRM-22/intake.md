# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/05-sla-automation/CRM-22/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** SLA automation
- **Feature slug (folder under `plans/`):** `05-sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-22` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
SLA escalation rules
```

---

## Description

```
As a supervisor, I want tickets to escalate automatically when SLA is at risk or breached, so that someone acts before the customer is affected.
```

---

## Acceptance criteria

```
1. When 80% of the response time has passed, the assignee is warned.
2. On breach, the ticket escalates to the supervisor (escalation level + 1) and a history entry is added.
3. The same escalation level is never triggered twice for the same ticket.
4. Resolving the ticket stops further escalations.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-19, CRM-20, CRM-21 (breach job), CRM-13 / CRM-14 (tickets).
- **Depends on code areas or other stories:** the CRM-21 recurring job (runs every minute), `Ticket.AssigneeId` (CRM-13 column; CRM-16 assigns), the seeded `Supervisor` role, `ResolvedAt` (set by CRM-17 later). CRM-18 (ticket history) is built later by another group.

## Extra notes (optional)

- In-app notifications UI is Phase 2: record warnings / escalations as ticket fields + a simple notification record and emit them through an `ISlaNotifier` interface (no-op / log implementation for now).
- Tests first (TDD).

## Technical hints (optional)

- Escalation level and "warned" flag live on the ticket; the job only moves them forward, so a level is never triggered twice.

## Out of scope

- Notification UI / e-mail delivery of warnings, configurable thresholds or escalation chains, multi-level escalation beyond the supervisor.
