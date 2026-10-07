# Story intake

- Folder: `.squad/stories/11-ai-features/CRM-53/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `11-ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-53`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 3`

---

## Title

```
AI: suggested solutions
```

---

## Description

```
As an agent, I want AI to suggest relevant knowledge base articles for a ticket, so that I find the solution quickly.
```

---

## Acceptance criteria

```
1. Shows the top 3 relevant articles for the ticket.
2. Only published articles are suggested.
3. The agent can insert a suggestion into the reply.
4. Useful / not useful feedback is stored per suggestion.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-50, CRM-51 (IKbRetriever), CRM-39 (insert article into reply).
- **Depends on code areas or other stories:** see technical hints.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/33-ai-suggested-solutions/`.

## Out of scope

- Learning from feedback, suggestions for customers.
