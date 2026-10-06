# Story intake

- Folder: `.squad/stories/09-customer-portal/CRM-43/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `09-customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-43`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Customer portal: access FAQs
```

---

## Description

```
As a customer, I want to browse FAQs and help articles in the portal, so that I can solve simple issues without opening a ticket.
```

---

## Acceptance criteria

```
1. FAQs and published articles are visible without signing in.
2. Search works in the portal.
3. Content follows the portal language (Arabic / English).
4. Each article has a "Was this helpful?" counter.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-36, 37, 38 (content + search).
- **Depends on code areas or other stories:** KB services of the knowledge-base feature.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/38-access-faqs-portal/`.
- Anonymous `/api/portal/kb/*` endpoints (categories, articles, article by id, faqs, search, `POST articles/{id}/feedback { helpful }`). Language comes from `Accept-Language` (the client sends the UI language); the other language is the fallback when a version is missing.
- Portal UI under `/portal`: home with search + FAQ list + articles by category, article page with the helpful buttons.

## Out of scope

- Per-visitor de-duplication of votes (the browser remembers its own vote), comments.
