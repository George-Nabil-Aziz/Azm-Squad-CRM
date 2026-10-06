# Story intake

- Folder: `.squad/stories/08-knowledge-base/CRM-37/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `08-knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-37`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Knowledge base: FAQs
```

---

## Description

```
As an admin, I want to manage FAQs (question and answer) in Arabic and English, so that customers get quick answers to common questions.
```

---

## Acceptance criteria

```
1. Admin can create, edit, and delete an FAQ.
2. A display order controls the order of FAQs.
3. Only published FAQs are returned to the portal.
4. Each FAQ supports Arabic and English.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-36 (permissions `kb.view` / `kb.manage`, KB area). Feeds CRM-38, CRM-43.
- **Depends on code areas or other stories:** CRM-36.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/26-browse-faqs/`.
- "Returned to the portal" = the public read model used by the portal (`/api/portal/kb/faqs`, anonymous); CRM-43 builds the portal UI on it. The staff list returns drafts only to `kb.manage`.

## Out of scope

- Search (CRM-38), portal pages (CRM-43), FAQ categories.
