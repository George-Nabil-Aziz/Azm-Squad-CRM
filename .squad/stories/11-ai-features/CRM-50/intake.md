# Story intake

- Folder: `.squad/stories/11-ai-features/CRM-50/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `11-ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-50`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 3`

---

## Title

```
AI: ticket summaries
```

---

## Description

```
As an agent, I want a one-click AI summary of a long ticket, so that I understand it quickly.
```

---

## Acceptance criteria

```
1. The summary is generated from the ticket thread in the ticket language (Arabic / English).
2. The summary is saved with a timestamp; regenerating replaces it.
3. If the AI call fails, an error is shown and the ticket is not affected.
4. Phone numbers and emails are masked before sending data to the AI.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-13..15 (tickets, thread).
- **Depends on code areas or other stories:** see technical hints.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/30-ai-ticket-summaries/`.
- Shared AI foundation created here and reused by CRM-51..54: `IAiTextService` (Application) with an Anthropic Messages API implementation over HttpClient (`Ai:ApiKey` secret, `Ai:Model` default claude-haiku-4-5-20251001), a fake in tests, 503 ProblemDetails when no key is configured, `GET /api/ai/status`, PII masking.

## Out of scope

- Automatic summaries, summaries of customers, streaming output.
