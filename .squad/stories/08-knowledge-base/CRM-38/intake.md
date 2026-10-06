# Story intake

- Folder: `.squad/stories/08-knowledge-base/CRM-38/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `08-knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-38`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Knowledge base: search
```

---

## Description

```
As an agent or customer, I want to search articles and FAQs, so that I find the right answer quickly.
```

---

## Acceptance criteria

```
1. Searching a keyword returns matching articles and FAQs ranked by relevance.
2. Arabic search works with normalization (أ/إ/آ = ا, ة = ه).
3. Drafts are never returned.
4. An empty query returns no results without error.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-36, CRM-37. Feeds CRM-43 (portal search).
- **Depends on code areas or other stories:** CRM-36, CRM-37.

## Technical hints (optional)

- CRM 01 reference (read-only): `specs/28-search-knowledge-base/`.
- Normalizer in Domain (unit tested): lower-case, أإآٱ→ا, ة→ه, strip diacritics and tatweel. A normalized `SearchText` column is stored on articles and FAQs; ranking is done in the Application layer.
- Staff endpoint `GET /api/kb/search?q=` (`kb.view`); the same service backs the anonymous portal search (CRM-43).

## Out of scope

- Full-text indexes, stemming, typo tolerance, search analytics.
