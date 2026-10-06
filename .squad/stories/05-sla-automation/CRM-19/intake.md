# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/05-sla-automation/CRM-19/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** SLA automation
- **Feature slug (folder under `plans/`):** `05-sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-19` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
SLA policy configuration (SuperAdmin)
```

---

## Description

```
As a SuperAdmin, I want to set response and resolution times per priority (High / Mid / Low), so that SLA targets match our service commitments.
```

---

## Acceptance criteria

```
1. Default SLA values are seeded for High, Mid, and Low.
2. SuperAdmin updates High to response = 1h, resolution = 4h -> values are saved.
3. A non-SuperAdmin updating SLA gets 403.
4. Values must be greater than 0 and resolution >= response, otherwise 400.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1..5 (foundation), CRM-6, CRM-7 (security-admin), CRM-12 (ticket priorities) — done.
- **Depends on code areas or other stories:** `Crm.Domain.Tickets.TicketPriority` + `TicketValues` (CRM-12), the permission `sla.manage` (CRM-7: SuperAdmin only), `PermissionPolicyTests` guards, the service + repository pattern of CRM-8 / CRM-12, the ticket-categories admin page as UI precedent.

## Extra notes (optional)

- Group D (SLA) is built on branch `feature/group-d-sla` in parallel with the ticket stories (CRM-13..18). CRM-19 needs only the priorities; CRM-20 (timers) reads this policy when tickets are created.
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code.

## Technical hints (optional)

- One policy row per priority (response time, resolution time), seeded by the migration.
- Admin UI reachable only with `sla.manage`; all strings in `ar` + `en`.

## Out of scope

- Business hours / calendars / holidays, per-customer or per-category SLA, applying the policy to tickets (CRM-20), breach detection (CRM-21), escalation (CRM-22).
