# Story intake

- Folder: `.squad/stories/11-ai-features/CRM-54/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `11-ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-54`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 3`

---

## Title

```
AI chatbot
```

---

## Description

```
As a customer, I want an AI chatbot that answers from the knowledge base and hands me to a human when needed, so that I get help instantly.
```

---

## Acceptance criteria

```
1. Answers only from published knowledge base articles and cites the article.
2. If the customer asks for an agent or confidence is low, it creates a ticket (or transfers to live chat) with the full transcript.
3. Works in Arabic and English.
4. When it does not know, it says so and offers an agent instead of inventing an answer.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-50, CRM-51 (IKbRetriever), CRM-40..43 (portal).
- **Depends on code areas or other stories:** see technical hints.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/34-ai-chatbot/`.

## Out of scope

- Live chat (no live chat exists: the hand-off is a ticket), chat history storage, voice.
