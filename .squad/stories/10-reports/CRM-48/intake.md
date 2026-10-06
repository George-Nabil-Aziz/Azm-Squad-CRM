# Story intake

- Folder: `.squad/stories/10-reports/CRM-48/intake.md`

---

## Feature

- **Feature name (display):** Reports
- **Feature slug (folder under `plans/`):** `10-reports`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-48`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Customer satisfaction report
```

---

## Description

```
As a manager, I want a customer satisfaction report, so that I can track service quality over time.
```

---

## Acceptance criteria

```
1. Shows average rating and the 1-5 distribution over time.
2. Can be grouped by agent and by category.
3. Lists low ratings (1-2) with their comments.
4. Shows the survey response rate %.
```

---

## Attachments

None.

---

## Dependencies

- **Related ids:** CRM-45 (shell, range), **CRM-44 (CSAT ratings) is being built on another branch** — not available here.
- **Decision for this branch:** define the read-side interface `ICsatReadModel` (Application) with an empty implementation (`EmptyCsatReadModel`); test the report logic with fakes. **Wire to CRM-44 on merge**: replace the DI registration with an EF implementation over the CRM-44 rating table.

## Technical hints (optional)

- CRM 01 reference (read-only, behaviour only): `specs/43-customer-satisfaction-reports/` (spec.md, contracts/api.md, tests/test-cases/43-customer-satisfaction-reports.md).
- Rating = 1..5 per resolved ticket with optional comment; response rate = rated tickets / surveys sent (or resolved tickets) — the read model returns both numbers.

## Out of scope

- Collecting ratings (CRM-44), the survey email / portal form.
