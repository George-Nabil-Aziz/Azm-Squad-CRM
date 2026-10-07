# Story intake

- Folder: `.squad/stories/11-ai-features/CRM-51/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `11-ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-51`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 3`

---

## Title

```
AI: suggested replies
```

---

## Description

```
As an agent, I want AI to suggest a reply draft, so that I answer faster.
```

---

## Acceptance criteria

```
1. A reply draft is suggested from the ticket thread and the knowledge base.
2. The draft is never sent automatically; the agent must review and send.
3. The draft language matches the customer's last message.
4. If the AI call fails, the agent can still reply normally.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-50 (AI foundation), CRM-36..39 (knowledge base).
- **Depends on code areas or other stories:** see technical hints.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/31-ai-suggested-replies/`.
- Reuses `IAiTextService` / `PiiMasker` / `TicketTranscript` from CRM-50; adds `IKbRetriever` (published articles matching any word of a text) reused by CRM-53 and CRM-54.

## Out of scope

- Sending drafts automatically, tone settings, learning from edits.
