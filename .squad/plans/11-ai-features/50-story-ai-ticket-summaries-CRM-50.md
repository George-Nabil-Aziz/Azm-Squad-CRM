# Story 50 — AI: ticket summaries (Story: CRM-50)

## Prerequisites

- Stories 13–15 (tickets, thread) and 36–39 (knowledge base) on `main`. CRM 01 reference (read-only): `specs/30-ai-ticket-summaries/`.
- This story creates the **shared AI foundation** used by 51–54 (see [00-overview.md](00-overview.md)). No new package: the Claude Messages API is called with `HttpClient`.

## Story Goal

1. **One-click summary** (AC 1): `POST /api/tickets/{id}/ai-summary` builds a transcript of the ticket (subject, description, every message, internal notes labelled), asks the AI for a short bullet summary **in the ticket language** (`AiLanguage.Detect`: Arabic letters vs Latin letters of the customer's text, subject as fallback) and returns it.
2. **Saved with a timestamp, regenerating replaces it** (AC 2): one `TicketAiSummary` row per ticket (`Text`, `Language`, `GeneratedAt`, `GeneratedById`); `GET /api/tickets/{id}/ai-summary` returns the saved one (all fields null when none).
3. **A failed AI call leaves the ticket and the old summary untouched** (AC 3): `AiFailedException` → ProblemDetails **502**, `AiNotConfiguredException` → **503** ("AI not configured"); nothing is saved. The UI shows the error toast (global `ApiErrorToaster`).
4. **Phone numbers and emails are masked before sending** (AC 4): `PiiMasker.Mask` replaces emails with `[email]` and phone numbers (7+ digits, Arabic-Indic digits too) with `[phone]` in everything that goes into the prompt. Customer names are never sent (messages are labelled Customer / Agent).

## Design (shared foundation)

- **Application `Crm.Application/Ai/`:** `AiOptions` (`Ai:ApiKey`, `Ai:Model` = `claude-haiku-4-5-20251001`, `Ai:ConfidenceThreshold` = 0.8, `Ai:TimeoutSeconds` = 30, `Ai:BaseUrl`), `IAiTextService { IsConfigured; CompleteAsync(AiRequest(System, User, MaxTokens)) }`, `AiNotConfiguredException`, `AiFailedException`, `PiiMasker`, `AiLanguage`, `AiText` (localized messages), `TicketTranscript` (builds + trims the transcript, newest messages kept), `ITicketSummaryService` / `ITicketSummaryRepository`.
- **Domain** `Crm.Domain/Ai/TicketAiSummary` (`Create`, `Replace`; UTC times from the caller).
- **Infrastructure:** `AnthropicTextService` (typed `HttpClient`, `POST {BaseUrl}/v1/messages`, headers `x-api-key` + `anthropic-version: 2023-06-01`, joins the `text` blocks of the answer; any non-2xx / timeout / bad JSON → `AiFailedException`; **never logs prompts or answers**, only the status code), `AiServiceCollectionExtensions` (options read lazily from configuration; a missing key only makes `IsConfigured` false), `TicketSummaryRepository`, configuration, migration `AddTicketAiSummaries`.
- **API** `Crm.Api/Endpoints/AiEndpoints.cs` (`MapAiEndpoints`): `GET /api/ai/status` (any signed-in user → `{ enabled }`), `GET` (`tickets.view`) and `POST` (`tickets.manage`) `/api/tickets/{id}/ai-summary`. `GlobalExceptionHandler` maps the two exceptions to 503 / 502 (`ErrorText` titles). **No new permission.**
- **Client:** `api/ai.ts`, `features/ai/useAi.ts` (`useAiStatus`), `features/ai/TicketSummary.tsx` on the ticket page (hidden without `tickets.view` or when AI is not enabled; button "Summarize" / "Regenerate", saved time shown), `ai.*` keys in `ar.json` / `en.json`.

## Backend Tasks

1. **Tests first (Red):** unit `PiiMaskerTests` (emails, `+966 50 123 4567`, `0501234567`, `(011) 234-5678`, Arabic-Indic digits, short numbers and `TKT-000012` untouched), `AiLanguageTests`, `TicketAiSummaryTests` (create, replace keeps one row, non-UTC rejected), `TicketSummaryServiceTests` (prompt has the language instruction, masked text and no customer name; saved with `GeneratedAt` = clock; second call replaces; AI throws → nothing saved and old summary kept; not configured → `AiNotConfiguredException`; unknown ticket → 404); integration `AiSummaryTests` with `FakeAiTextService` (generate, GET returns it, regenerate replaces and updates the timestamp, failing AI → 502 and the ticket and old summary unchanged, no key → 503, status endpoint, Agent may POST, anonymous 401, prompt sent to the fake contains no email / phone) and `AnthropicTextServiceTests` (stub `HttpMessageHandler`: request shape, text extracted, 401 / 500 / timeout → `AiFailedException`).
2. **Implement** Domain, Application, Infrastructure (+ migration `AddTicketAiSummaries`), API, the exception mapping.

## Frontend Tasks

1. **Tests first:** `api/ai.test.ts`, `features/ai/TicketSummary.test.tsx` (button generates and shows the summary and time, regenerate replaces, hidden when disabled, error keeps the old summary), ticket page shows the panel.
2. Implement `api/ai.ts`, `useAi.ts`, `TicketSummary.tsx`, i18n.

## Edge cases

- A very long thread is trimmed to the newest messages (about 12 000 characters) after masking; subject and description are always kept.
- Two agents regenerating at once: last write wins (one row per ticket, unique `TicketId`).
- Tests never call the real API: `CrmApiFactory` hosts register `FakeAiTextService` (`ConfigureTestServices`); without a registration the key is empty and the service reports "not configured".

## Out of scope

Automatic summaries, summary history, streaming.

## Verification / done

`dotnet build` (0 warnings), `dotnet test`, `npm test`, `npm run build`, `npm run lint`; AC 1–4 each have a test; migration `AddTicketAiSummaries`.

## As built

- Shared foundation as planned (`Crm.Application/Ai/AiCore.cs`, `TicketSummaryService.cs`; `Crm.Infrastructure/Ai/`). `appsettings.json` carries only the non-secret `Ai:Model`, `Ai:ConfidenceThreshold`, `Ai:TimeoutSeconds`; the key is set with `dotnet user-secrets set "Ai:ApiKey" "<key>"` (Api project) or the environment variable `Ai__ApiKey`.
- Migration `AddTicketAiSummaries`. `TicketsAuthorizationTests` lists the two new `/api/tickets/{id}/ai-summary` routes.
- The ticket page test mocks `@/api/ai` (AI disabled by default) and has one test for the panel.
