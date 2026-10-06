# Story intake

- Folder: `.squad/stories/08-knowledge-base/CRM-36/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `08-knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-36`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Knowledge base: articles & categories
```

---

## Description

```
As a knowledge base editor, I want to write help articles in categories, in Arabic and English, so that agents and customers can find answers themselves.
```

---

## Acceptance criteria

```
1. Creating an article with title, body, and category saves it as Draft.
2. Publishing makes it visible; drafts are hidden from agents' search and the portal.
3. An article can have Arabic and English versions.
4. Missing title returns 400.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-7 (permission catalogue), CRM-5 (ProblemDetails). Feeds CRM-37, 38, 39, 43.
- **Depends on code areas or other stories:** `Crm.Application/Auth/Permissions.cs`, `RolePermissions.cs`.

## Technical hints (optional)

- CRM 01 reference (read-only, not copied): `specs/27-help-articles-guides/`, `specs/29-manage-kb-content/`.
- New permissions `kb.view` (agents: read published articles) and `kb.manage` (editors: write, see drafts).
- Two language versions as columns (`TitleEn/BodyEn/TitleAr/BodyAr`); at least one complete version.

## Out of scope

- Search (CRM-38), FAQs (CRM-37), linking to tickets (CRM-39), portal pages (CRM-43), attachments / rich-text editor, article version history.
