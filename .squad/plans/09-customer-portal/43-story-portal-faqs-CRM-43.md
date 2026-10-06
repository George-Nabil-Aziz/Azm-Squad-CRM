# Story 43 — Customer portal: access FAQs (Story: CRM-43)

## Prerequisites

- Stories 36–38 (articles, FAQs, search, `PortalKbEndpoints` with `faqs` and `search` already anonymous) and 40 (portal layout).
- CRM 01 reference (read-only): `specs/38-access-faqs-portal/`.

## Story Goal

1. **FAQs and published articles are visible without signing in** (AC 1): anonymous `GET /api/portal/kb/categories`, `/articles`, `/articles/{id}`, `/faqs`. A draft article is 404, drafts / unpublished FAQs never appear.
2. **Search works in the portal** (AC 2): the existing anonymous `GET /api/portal/kb/search?q=` (CRM-38) with a search box on the portal home page.
3. **Content follows the portal language** (AC 3): the server picks the version from `Accept-Language` (the client sends the UI language) and falls back to the other language; switching the language in the portal reloads the content.
4. **"Was this helpful?" counter** (AC 4): `POST /api/portal/kb/articles/{id}/feedback { helpful }` raises `HelpfulCount` / `NotHelpfulCount` of a published article and returns the counts; non-boolean / missing `helpful` → 400, draft or unknown → 404. The article page shows the buttons and the counts; the browser remembers its own vote.

## Design

- **Domain** `KbArticle.RecordFeedback(helpful)` (published only, throws otherwise).
- **Application** `IPortalKbService` (`ListCategoriesAsync` = categories with their published article count, `ListArticlesAsync(categoryId, page, pageSize)`, `GetArticleAsync`, `RecordFeedbackAsync`); `IKbArticleRepository.CountPublishedByCategoryAsync`. Responses: `PortalKbCategoryResponse(Id, Name, ArticleCount)`, `PortalKbArticleSummary(Id, Title, Summary, CategoryName)`, `PortalKbArticleResponse(Id, Title, Body, CategoryName, HelpfulCount, NotHelpfulCount, PublishedAt)`, `PortalFeedbackRequest(Helpful)`.
- **API** `PortalKbEndpoints` (anonymous). **No migration** (counters exist since CRM-36).

## Backend Tasks

1. Tests first: unit `KbArticleFeedbackTests`, `PortalKbServiceTests` (categories without published articles are hidden, drafts hidden, language choice and fallback, feedback counters, draft feedback 404, missing `helpful` 400); integration `PortalKbTests` (anonymous 200 for categories / articles / article / faqs / search; draft article 404; Arabic `Accept-Language`; feedback increments and shows in the staff list `helpfulCount`; invalid body 400).
2. Implement.

## Frontend Tasks

1. Tests first: portal home shows search, FAQs and categories without a portal login; searching lists results; the article page shows the body and the helpful buttons, a click posts feedback and shows the thank-you and the counts; switching language refetches.
2. `api/portal-kb.ts`, `PortalHomePage` (replaces the placeholder), `PortalArticlePage` (`/portal/kb/articles/:id`, public), i18n.

## Edge cases

- Votes are not de-duplicated on the server (a visitor could vote repeatedly); the browser stores the vote per article in `localStorage` (failure-tolerant) and disables the buttons afterwards.
- A category whose articles are all drafts is not listed.

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test.
