# Story 51 — AI: suggested replies (Story: CRM-51)

## Prerequisites

- [Story 50](50-story-ai-ticket-summaries-CRM-50.md) done (`IAiTextService`, `PiiMasker`, `AiLanguage`, `TicketTranscript`, 503 / 502 handling, `GET /api/ai/status`, `api/ai.ts`, `useAiStatus`).
- Stories 36–39 (knowledge base: `KbArticle.SearchText`, `KbSearchText`). CRM 01 reference (read-only): `specs/31-ai-suggested-replies/`.

## Story Goal

1. **A draft from the thread and the knowledge base** (AC 1): `POST /api/tickets/{id}/ai-reply-draft` (`tickets.manage`) builds the customer-visible thread (internal notes are **not** sent: the draft goes to the customer), looks up the top 3 published articles with `IKbRetriever` (subject + latest customer message + description) and asks the AI for a reply that uses them. Response `{ draft, language, articles: [{ id, title }] }` (the articles the draft was based on).
2. **Never sent automatically** (AC 2): the endpoint only returns text. It creates no message, dispatches nothing and does not touch the ticket (test: thread, outbound messages and timeline unchanged). The UI puts the draft into the reply box (appended after a blank line when there is already text) with the note "Review before sending"; only the existing Send button sends.
3. **Language of the customer's last message** (AC 4 of the story text: "language matches"): `AiLanguage.Detect` on the newest inbound message; description, then subject when there is none.
4. **A failed AI call does not block replying** (AC 4): 502 / 503 ProblemDetails, error toast, the reply box keeps what the agent typed and sending works. Without a key the button is hidden (`GET /api/ai/status`).

## Design

- **Application** `KnowledgeBase/KbRetriever.cs`: `IKbRetriever.FindAsync(text, max, language, ct)` → `KbRetrievedArticle(Id, Title, Body, Score)` in the language asked (fallback to the other version). Terms = distinct normalized words (`KbSearchText.Normalize`) of 3+ characters minus a small English / Arabic stop-word list, at most 12; `IKbRetrievalRepository.FindPublishedByTermAsync(term, max)` (published, non-deleted; `LIKE` on `SearchText`, one query per term); score = 3 per term in the title + 1 per term in the body; ties → newest first. **Only published, non-deleted articles are ever returned.**
- **Application** `Ai/ReplyDraftService.cs`: `IReplyDraftService.SuggestAsync(ticketId)`; prompt = system (write in {Language}; use the articles when they apply; do not invent prices, dates or policies; ask for missing information; greeting + sign-off with `[Agent name]`; draft only) + masked transcript (`TicketTranscript.Build` over the public messages) + the articles (title and body cut to 1 500 characters; they are public content, not masked).
- **Infrastructure** `KbRetrievalRepository`. **API** `POST /api/tickets/{id:guid}/ai-reply-draft` in `AiEndpoints.cs`.
- **Client** `generateReplyDraft` in `api/ai.ts`; `features/ai/SuggestReplyButton.tsx` in `TicketReplyForm` (next to "Insert article"); i18n `ai.reply.*`.

## Backend Tasks

1. **Tests first (Red):** unit `KbRetrieverTests` (published only, deleted / draft never, any-term match, title outranks body, stop words and short words ignored, Arabic normalization, limit, language fallback), `ReplyDraftServiceTests` (articles and masked thread in the prompt, internal notes excluded, language from the last customer message — Arabic after an English first message and the other way round, draft returned with its sources, nothing saved / sent, AI failure and not configured propagate, unknown ticket 404, ticket without a message uses description / subject); integration `AiReplyDraftTests` (draft returned; a draft article is not in the prompt, a published one is; thread / messages count unchanged and nothing dispatched; 502 and 503 leave the ability to reply: `POST /messages` still 201 afterwards; Agent 200, no `tickets.manage` 403, anonymous 401) and `KbRetrievalRepositoryTests` through the endpoint (published article found by a word of the ticket).
2. **Implement** retriever, repository, service, endpoint, DI.

## Frontend Tasks

1. **Tests first:** `api/ai.test.ts` (POST path), `features/ai/SuggestReply.test.tsx` (draft fills the box, appends to typed text, sources listed, "review" note, a failure keeps the typed text and Send still works, hidden when AI is off or without `tickets.manage`).
2. Implement the button, wire it into `TicketReplyForm`, i18n.

## Edge cases

- No matching articles: the draft is still generated from the thread, `articles` is empty and the prompt says no knowledge base article was found (the AI is told not to invent policy).
- A closed ticket takes no replies; the button is only rendered with the reply form.
- Prompts contain no customer name; the draft may contain `[email]` / `[phone]` placeholders when the customer wrote them, the agent edits them before sending.

## Out of scope

Auto-send, saving drafts, tone / length settings.

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test.

## As built

- `KbRetriever` (+ `IKbRetrievalRepository`, `KbRetrievalRepository`) and `ReplyDraftService`; `POST /api/tickets/{id}/ai-reply-draft` (`tickets.manage`). No migration, no new permission. `TicketsAuthorizationTests` lists the new route.
- The language comes from the newest customer message (`AiLanguage.Detect`), description then subject when there is none; the knowledge base is searched in that language.
- Existing tests that render the reply form mock `@/api/ai` (AI disabled).
