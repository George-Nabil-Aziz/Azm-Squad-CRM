# Story 36 — Knowledge base: articles & categories (Story: CRM-36)

## Prerequisites

- Stories 01–11 done (auth, permission catalogue CRM-7, ProblemDetails CRM-5, app layout CRM-3, i18n CRM-4).
- CRM 01 reference (read-only, not copied): `specs/27-help-articles-guides/`, `specs/29-manage-kb-content/`.

## Story Goal

1. Editors (`kb.manage`) create **categories** and **articles**; an article has an English and/or Arabic version (title + body), belongs to a category and is saved as **Draft** (AC 1, AC 3).
2. **Publish / unpublish** an article. Readers (`kb.view`) only ever get **published** articles; drafts are 404 / absent for them (AC 2). The portal (CRM-43) and search (CRM-38) reuse the same rule.
3. A missing title (no title in any language), or a title without body (and vice versa) → **400** with field errors (AC 4).
4. Client: "Knowledge base" area with Articles and Categories tabs (agents read published articles, editors manage).

## Context — read these files first

`CLAUDE.md`; `server/src/Crm.Application/Auth/{Permissions,RolePermissions}.cs`; `Tickets/TicketCategory*` (category pattern) and `TicketCategoriesEndpoints.cs`; `Persistence/Configurations/CustomerConfiguration.cs` (soft-delete filter); `client/src/pages/ticket-categories/` and `features/ticket-categories/` (table + dialog pattern).

## Design

- **Domain** `Crm.Domain/KnowledgeBase/`: `KbCategory` (NameEn, NameAr; at least one), `KbArticle` (CategoryId, TitleEn/BodyEn/TitleAr/BodyAr, `Status` Draft|Published, `PublishedAt`, `HelpfulCount`, `NotHelpfulCount`, `LinkedCount`, soft delete). Methods `Create`, `Update`, `Publish`, `Unpublish`, `Delete`; all times UTC from the caller.
- **Application** `Crm.Application/KnowledgeBase/`: `IKbCategoryService`, `IKbArticleService`, repositories, DTO records, FluentValidation validators, `KbText` (en + ar), `KbLanguage` (picks the request language, falls back to the other). Article list for a user without `kb.manage` is forced to published.
- **Permissions:** `kb.view` (Agent, Supervisor, Admin, SuperAdmin), `kb.manage` (Admin, SuperAdmin).
- **API** `KbEndpoints.cs`: `GET/POST /api/kb/categories`, `PUT/DELETE /api/kb/categories/{id}` (delete → 409 while it has articles); `GET/POST /api/kb/articles`, `GET/PUT/DELETE /api/kb/articles/{id}`, `POST /api/kb/articles/{id}/publish|unpublish`. Reads need `kb.view`, writes `kb.manage`.
- **Persistence:** configurations + migration `AddKnowledgeBase` (generated).

## Backend Tasks

### 1 — Tests first (Red)
Unit (`tests/Crm.UnitTests/KnowledgeBase/`): `KbArticleTests` (create → Draft; publish sets `PublishedAt`; unpublish; one-language article valid; no title throws), `KbArticleRequestValidatorTests` (missing title → error on `title`; title without body; too long), `KbArticleServiceTests` (create saves Draft; non-manager list/get never see drafts; unknown category → 400; publish/unpublish), `KbCategoryServiceTests` (delete with articles → conflict). Update `RolePermissionsTests` / Agent permission lists for `kb.view`.
Integration (`tests/Crm.Api.IntegrationTests/KnowledgeBase/`): `KbArticlesTests` (AC 1–4 over HTTP: create → 201 + `status: draft`; publish; agent GET list/detail sees published only, draft detail → 404; bilingual article; missing title → 400 ProblemDetails with `errors.title`), `KbAuthorizationTests` (anonymous 401, agent write 403, agent read 200).

### 2 — Domain + Application + Infrastructure + Api (Green)
Create the files above, register in `DependencyInjection` (Application / Infrastructure), `app.MapKbEndpoints()`, migration `AddKnowledgeBase`, then `has-pending-model-changes` = "No changes".

## Frontend Tasks

1. Tests first: `KnowledgeBasePage.test.tsx` — editor creates an article (form fields en + ar, category select) and sees it as Draft; publish button; agent (no `kb.manage`) sees no create button; a validation 400 shows the field error.
2. `client/src/api/knowledge-base.ts`, `features/knowledge-base/` (`ArticlesTable`, `ArticleFormDialog`, `CategoriesTable`, `CategoryFormDialog`, hooks, zod schema), `pages/knowledge-base/KnowledgeBasePage.tsx`; replace the ComingSoon route; sidebar item gets `permission: kbView`; `permissions.ts` + i18n `knowledgeBase.*` (en + ar).

## Edge cases

- Whitespace-only title = missing. Unpublishing a published article makes it disappear for readers immediately.
- Deleting an article is a soft delete (hidden everywhere). Deleting a category that still has (non-deleted) articles → 409.
- An article whose category is later deleted cannot happen (blocked above).

## Test plan / verification

Server: `dotnet build`, `dotnet test`. Client: `npm test`, `npm run build`, `npm run lint`.

## Done criteria

- [ ] AC 1 Draft on create; [ ] AC 2 publish visibility; [ ] AC 3 two languages; [ ] AC 4 400 on missing title; [ ] all builds / tests green.

## As built

- Deviation: categories are soft-deleted (`ISoftDeletable`) instead of physically removed, so articles keep a valid reference; `IgnoreQueryFilters` switches the named filter off for the whole query, so the article read model excludes deleted articles by hand.
- Deviation: the sidebar item and the `/knowledge-base` route need `kb.view` (the Agent role got `kb.view`; `App.layout.test` no longer lists the area as "coming soon"; `fake-api` `agentMe` includes `kb.view`).
- Migration `AddKnowledgeBase` (tables `KbCategories`, `KbArticles`). New permissions `kb.view` (Agent, Supervisor, Admin, SuperAdmin), `kb.manage` (Admin, SuperAdmin). FAQs tab arrives with CRM-37.
- Deviation (merge of main): the six branch migrations (`AddKnowledgeBase`, `AddKbFaqs`, `AddKbSearchText`, `AddTicketArticleLinks`, `AddPortalAuth`, `AddTicketAttachments`) interleaved with main's migrations and its snapshot, so (only those, never main's) they were replaced by one migration `AddKnowledgeBaseAndPortal` generated on top of main's snapshot.
