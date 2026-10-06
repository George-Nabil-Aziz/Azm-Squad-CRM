# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/05-sla-automation/CRM-20/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** SLA automation
- **Feature slug (folder under `plans/`):** `05-sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-20` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
SLA timers on tickets
```

---

## Description

```
As an agent, I want each ticket to show its SLA due times, so that I know how much time is left.
```

---

## Acceptance criteria

```
1. On creation, ResponseDueAt and ResolutionDueAt are calculated from the SLA policy of the ticket priority.
2. Changing the priority recalculates the due times.
3. The ticket shows remaining time (or overdue) for response and resolution.
4. Changing the SLA policy does not change due times of existing tickets.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-19 (SLA policy), CRM-13 (create ticket), CRM-14 (ticket list) — CRM-13/14 are built in parallel on `feature/group-b-tickets` and merged into this branch first.
- **Depends on code areas or other stories:** `SlaPolicy` + `ISlaPolicyRepository` (CRM-19), the `Ticket` entity / `TicketService` / `TicketView` / `TicketResponse` (CRM-13), the tickets page (CRM-13/14). CRM-15 (replies → `FirstResponseAt`) and CRM-17 (status → `ResolvedAt`, priority change) come later from another group.

## Extra notes (optional)

- `FirstResponseAt` / `ResolvedAt` (nullable UTC) are added to `Ticket` by this story if missing, so the remaining-time display can stop once met; CRM-15 / CRM-17 set them.
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code.

## Technical hints (optional)

- Due times are stored on the ticket (copied from the policy at creation), never recomputed from the current policy.
- Remaining time is computed from the server clock (`TimeProvider`) — testable with the fake clock.

## Out of scope

- Breach flags and the recurring job (CRM-21), warnings and escalation (CRM-22), business hours, pausing the SLA while a ticket is pending.
