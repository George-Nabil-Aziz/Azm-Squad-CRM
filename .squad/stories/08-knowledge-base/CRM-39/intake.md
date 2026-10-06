# Story intake

- Folder: `.squad/stories/08-knowledge-base/CRM-39/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `08-knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-39`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Link article to ticket
```

---

## Description

```
As an agent, I want to insert a knowledge base article into a ticket reply, so that I share a ready solution with the customer.
```

---

## Acceptance criteria

```
1. Inserting an article adds its link and summary to the reply.
2. The ticket records which articles were linked.
3. Each article stores how many times it was linked (for reports).
4. Linking an unpublished article returns 400.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-36 (articles), CRM-15 (ticket replies, reply form).
- **Depends on code areas or other stories:** `TicketReplyForm`, ticket details page.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/29-manage-kb-content/`.
- `POST /api/tickets/{id}/articles { articleId, language? }` (`tickets.manage`) records the link, bumps `LinkedCount` and returns the text to insert (title, summary, portal link). `GET /api/tickets/{id}/articles` lists linked articles.
- Reply form: "Insert article" picker (search published articles) appends the returned text to the reply box.

## Out of scope

- Reports on link counts (reports feature), automatic suggestions (AI feature).
