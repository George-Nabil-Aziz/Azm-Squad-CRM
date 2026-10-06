# knowledge-base — plan overview

Entry point for the **knowledge-base** feature (help articles in categories, FAQs, search, linking articles into ticket replies; Arabic + English). Stories execute in order by their `NN` prefix; `NN` continues the global sequence.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 36 | [36-story-kb-articles-categories-CRM-36.md](36-story-kb-articles-categories-CRM-36.md) | Knowledge base: articles & categories | CRM-36 | 01–07 |
| 37 | [37-story-kb-faqs-CRM-37.md](37-story-kb-faqs-CRM-37.md) | Knowledge base: FAQs | CRM-37 | 36 |
| 38 | [38-story-kb-search-CRM-38.md](38-story-kb-search-CRM-38.md) | Knowledge base: search | CRM-38 | 36, 37 |
| 39 | [39-story-link-article-to-ticket-CRM-39.md](39-story-link-article-to-ticket-CRM-39.md) | Link article to ticket | CRM-39 | 36, 15 |

## Dependency notes

- **Shared contracts created here:** Domain `Crm.Domain/KnowledgeBase/` (`KbCategory`, `KbArticle`, `KbFaq`, `KbSearchText`); Application `Crm.Application/KnowledgeBase/` (services, DTOs, `KbText`, `KbLanguage`); Infrastructure repositories + EF configurations; Api `KbEndpoints` (`/api/kb/*`).
- **Permissions (CRM-7 catalogue):** `kb.view` (Agent, Supervisor, Admin, SuperAdmin: read published content, search) and `kb.manage` (Admin, SuperAdmin: write, see drafts). Mirrored in `client/src/auth/permissions.ts`.
- **Two languages:** every article / FAQ / category keeps `...En` and `...Ar` columns; at least one complete version is required. Read models for readers (portal, search) pick the request language and fall back to the other one.
- **Drafts** are visible only to `kb.manage`. Everything reader-facing (agent search, ticket linking, portal in [09-customer-portal](../09-customer-portal/00-overview.md)) only ever sees published content.
- **Client:** the "Knowledge base" sidebar area (`/knowledge-base`) replaces the coming-soon page: tabs Articles / FAQs / Categories.
