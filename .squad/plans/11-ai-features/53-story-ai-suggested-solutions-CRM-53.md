# Story 53 — AI: suggested solutions (Story: CRM-53)

## Prerequisites

- Stories [50](50-story-ai-ticket-summaries-CRM-50.md) and [51](51-story-ai-suggested-replies-CRM-51.md) done (`IAiTextService`, `IKbRetriever`, `AiLanguage`), story 39 (`POST /api/tickets/{id}/articles`, `InsertArticleControl`). CRM 01 reference (read-only): `specs/33-ai-suggested-solutions/`.

## Story Goal

1. **Top 3 relevant articles** (AC 1): `GET /api/tickets/{id}/ai-suggestions` (`tickets.view` + `kb.view`) retrieves up to 8 candidates with `IKbRetriever` (subject, description, latest customer message; language of the customer text). When AI is configured it picks and orders the best 3 (JSON `{ "articleIds": [...] }`, only candidate ids count); without AI, on a failure or an unusable answer the keyword ranking is used. Result `[{ articleId, title, summary, useful }]` (`useful` = the current user's vote or null).
2. **Only published articles** (AC 2): candidates come from the retriever (published, non-deleted only); drafts and deleted articles can never appear, also not by AI id.
3. **Insert into the reply** (AC 3): the panel's "Insert" button reuses `linkTicketArticle` (CRM-39) and appends its `insertText` to the reply box.
4. **Feedback per suggestion** (AC 4): `PUT /api/tickets/{id}/ai-suggestions/{articleId}/feedback` (`tickets.manage` + `kb.view`) body `{ useful: bool }` stores one `TicketSuggestionFeedback` per (ticket, article, user); voting again replaces the vote. A draft / unknown article → 400 on `articleId`, unknown ticket 404, missing `useful` 400.

## Design

- Domain `TicketSuggestionFeedback` (`Create`, `Set`); Application `Ai/SuggestedSolutionsService.cs` (`ISuggestedSolutionsService`, `ISuggestionFeedbackRepository`); Infrastructure repository + configuration (unique index ticket + article + user) + migration `AddTicketSuggestionFeedback`; endpoints in `AiEndpoints.cs`; client `getSuggestions` / `sendSuggestionFeedback` in `api/ai.ts`, `features/ai/SuggestedSolutions.tsx` on the ticket page (needs `kb.view`; the keyword fallback means it is shown even when AI is off), i18n `ai.suggestions.*`. No new permission.

## Tasks (tests first)

- Backend tests: unit `SuggestedSolutionsServiceTests` (top 3 only, drafts excluded, AI order respected, AI failure / not configured / garbage → keyword fallback, foreign ids ignored, feedback stored / replaced / per user, draft article 400, unknown ticket 404, `useful` listed); integration `AiSuggestionsTests` (3 of 5 published returned, draft never, feedback persisted and shown on the next GET, 403 without `kb.view`: Admin-less role not available, so route policies are asserted in `TicketsAuthorizationTests`, 401 anonymous). Then implement.
- Frontend tests: `SuggestedSolutions.test.tsx` (3 articles, insert calls `linkTicketArticle` and `onInsert`, useful / not useful calls the API and marks the choice, hidden without `kb.view`, empty text). Then implement.

## Out of scope

Learning from votes, suggestions to customers, showing in the portal.
