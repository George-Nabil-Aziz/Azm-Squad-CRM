# Story 39 — Link article to ticket (Story: CRM-39)

## Prerequisites

- Story 36 (articles), 38 (search used by the picker), CRM-15 (reply form `TicketReplyForm`, `TicketMessageService`).
- CRM 01 reference (read-only): `specs/29-manage-kb-content/`.

## Story Goal

1. **Inserting an article** into a reply adds its **link and summary** to the reply text (AC 1): the client asks the API, which returns the text to insert (title, summary of ≤ 200 characters, portal link) and the editor appends it to the reply box.
2. **The ticket records which articles were linked** (AC 2): table `TicketArticleLinks` (ticket, article, agent, time); `GET /api/tickets/{id}/articles` lists them; the ticket details page shows them.
3. **Each article stores how many times it was linked** (AC 3): `KbArticle.LinkedCount` is raised by every insertion (shown in the articles table, used by reports later).
4. **Linking an unpublished (draft) article returns 400** on `articleId` (AC 4); unknown article → 404, unknown ticket → 404, missing `articleId` → 400.

## Design

- **Domain** `TicketArticleLink` (`Crm.Domain/KnowledgeBase`), `KbArticle.RecordLinked()` (+1; throws for a draft / deleted article).
- **Application** `ITicketArticleService` (`LinkAsync`, `ListAsync`), `ITicketArticleRepository`, DTOs `LinkArticleRequest(ArticleId, Language?)`, `LinkedArticleResponse(Id, ArticleId, Title, Summary, Url, InsertText, LinkedAt)`, `TicketArticleResponse`. `Language` ("en" | "ar", default the request language, fallback to the other version). `InsertText` = `"\n\n{title}\n{summary}\n{url}\n"`. `PortalOptions` (`BaseUrl`, `ReopenWindowDays`, `SurveyValidDays`) read from `Portal:*` configuration (lazy singleton); `Url` = `{BaseUrl}/portal/kb/articles/{id}` (relative when `BaseUrl` is empty).
- **API** `TicketArticlesEndpoints.cs`: `POST /api/tickets/{id}/articles` (`tickets.manage` + `kb.view`), `GET` (`tickets.view`).
- **Migration** `AddTicketArticleLinks`.

## Backend Tasks

1. Tests first: unit `KbArticleLinkTests` (`RecordLinked` raises the counter; draft throws), `TicketArticleServiceTests` (published article → text with title, summary, link; counter +1 and link row; twice → 2; draft → `ValidationException` on `articleId`, counter unchanged; unknown ticket / article → NotFound; missing id → validation; language selection; summary cut at 200), integration `TicketArticlesTests` (AC 1–4 over HTTP, list of linked articles, agent allowed, user without `tickets.manage` 403).
2. Implement domain, application, repository, configuration, migration, endpoints, `PortalOptions` registration.

## Frontend Tasks

1. Tests first (`InsertArticle.test.tsx`): in the reply form "Insert article" opens a search; choosing a result calls the API and appends the returned text to the reply box; the control is hidden without `kb.view`; the details page lists linked articles.
2. `api/tickets.ts` (`linkTicketArticle`, `listTicketArticles`), `features/tickets/InsertArticleControl.tsx`, `LinkedArticles.tsx`, i18n en + ar.

## Edge cases

- Linking the same article twice counts twice (every insertion is a use). An article unpublished after being linked stays in the ticket's list. Whitespace-only summary falls back to the title only.

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test.

## As built

- Migration `AddTicketArticleLinks`. `appsettings.json` got a non-secret `Portal` section (`BaseUrl`, `ReopenWindowDays`, `SurveyValidDays`); `PortalOptions` is resolved lazily (Infrastructure `AddPortalOptions`). `TicketsAuthorizationTests` counts the two new `/api/tickets/{id}/articles` endpoints. The insert control lists articles only (FAQs are excluded from the picker). No other deviations.
