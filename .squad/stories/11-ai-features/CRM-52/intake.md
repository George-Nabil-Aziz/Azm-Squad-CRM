# Story intake

- Folder: `.squad/stories/11-ai-features/CRM-52/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `11-ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-52`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 3`

---

## Title

```
AI: automatic categorization
```

---

## Description

```
As a supervisor, I want new tickets categorized and prioritized automatically by AI, so that they reach the right agent faster.
```

---

## Acceptance criteria

```
1. A new ticket gets a suggested category and priority with a confidence score.
2. Above the confidence threshold it is applied automatically; below it is shown as a suggestion only.
3. When an agent overrides it, the override is recorded.
4. If AI is unavailable, the ticket is created normally.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-50 (AI foundation), CRM-12 (categories), CRM-13 (create ticket), CRM-17/20 (priority, SLA), CRM-27 (auto-assignment).
- **Depends on code areas or other stories:** see technical hints.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/32-automatic-categorization/`.
- `TicketService.CreateCoreAsync` is the one place every ticket (agent, email, WhatsApp, portal) is created; the classifier runs there before the SLA timers and the auto-assignment.

## Out of scope

- Retraining or feedback loops, re-classifying existing tickets.
