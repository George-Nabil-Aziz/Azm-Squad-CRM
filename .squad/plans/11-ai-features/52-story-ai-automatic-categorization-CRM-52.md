# Story 52 — AI: automatic categorization (Story: CRM-52)

## Prerequisites

- [Story 50](50-story-ai-ticket-summaries-CRM-50.md) done (`IAiTextService`, `PiiMasker`, `AiOptions.ConfidenceThreshold`, 503 / 502).
- Stories 12 (categories), 13 (create), 17 / 20 (priority change, SLA), 27 (auto-assignment) on `main`. CRM 01 reference (read-only): `specs/32-automatic-categorization/`.

## Story Goal

1. **Suggested category and priority with a confidence score** (AC 1): when a ticket is created (`TicketService.CreateCoreAsync`: agents, email, WhatsApp, portal) the subject and description (masked) go to the AI with the **active categories** (id + name). It answers JSON `{ categoryId, priority, confidence }`; an unknown category id counts as "no category", the confidence is clamped to 0..1, an answer that cannot be parsed counts as "AI unavailable". The result is stored in `TicketAiClassification` (one row per ticket).
2. **Threshold** (AC 2): `confidence >= Ai:ConfidenceThreshold` (default 0.8) → applied automatically to the ticket **before** the SLA timers and the auto-assignment, so due times and routing use the final values; below it → stored as a suggestion only (the ticket gets the normal defaults). A value the creator chose explicitly (a category in the request, a priority in the request) is never replaced by AI; the suggestion is still stored.
3. **Override recorded** (AC 3): when anybody changes the category or priority of a ticket that has a classification to a value that differs from the AI suggestion (`PUT .../category`, `PUT .../priority`, also when the suggestion was only shown), `CategoryOverriddenAt/To` or `PriorityOverriddenAt/To` and `OverriddenById` are set; changing it back to the AI value clears the override.
4. **AI unavailable → ticket created normally** (AC 4): no key, a provider error, a timeout (10 s) or an unparsable answer give no classification and no error: the ticket is created exactly as before.

`GET /api/tickets/{id}/ai-classification` (`tickets.view`) returns the stored result for the ticket page.

## Design

- **Domain** `Crm.Domain/Ai/TicketAiClassification`: `Create(ticketId, suggestedCategoryId, suggestedPriority, confidence, categoryApplied, priorityApplied, utcNow)`, `RecordCategoryChange(newCategoryId, userId, utcNow)`, `RecordPriorityChange(newPriority, userId, utcNow)`; `Status` = applied (anything applied) | suggested.
- **Application** `Ai/AiClassificationService.cs`: `IAiClassificationService` — `ClassifyAsync(subject, description)` → `AiClassificationOutcome?` (null = unavailable; never throws except cancellation), `Save(ticketId, outcome, categoryApplied, priorityApplied, now)` (adds to the unit of work), `RecordCategoryChangeAsync` / `RecordPriorityChangeAsync` (used by `TicketCategoryChangeService` and `TicketService.ChangePriorityAsync`; no-ops without a classification), `GetAsync(ticketId)` → `TicketAiClassificationResponse` (`Status` "none" | "applied" | "suggested", suggested category id / name, priority, confidence, applied flags, override times). `TicketService`, `TicketCategoryChangeService` get the service as an **optional trailing constructor parameter** (older unit tests keep compiling).
- **Infrastructure** `AiClassificationRepository`, configuration `TicketAiClassificationConfiguration`, migration `AddTicketAiClassifications`.
- **API** `GET /api/tickets/{id:guid}/ai-classification` in `AiEndpoints.cs`. **No new permission.**
- **Client** `getTicketClassification` in `api/ai.ts`; `features/ai/TicketAiClassification.tsx` on the ticket page (below the classify controls): shows "AI suggested category X, priority Y (confidence 92 %), applied automatically | suggestion only", the override note, and for a suggestion only an "Apply suggestion" button (`tickets.manage`) that uses the existing category / priority endpoints. i18n `ai.classification.*`.

## Backend Tasks

1. **Tests first (Red):** unit `TicketAiClassificationTests` (create, status, override recorded / cleared when it returns to the AI value, same value is not an override), `AiClassificationServiceTests` (prompt lists only active categories and masks personal data; JSON parsing incl. fenced / text-wrapped JSON, unknown category → none, bad priority / garbage / empty → null, confidence clamped; not configured → null without a call; provider failure → null; timeout → null; threshold at, above and below 0.8; override tracking; get), `TicketServiceAiTests` (confident → category and priority applied and SLA due time from the AI priority; below threshold → defaults and suggestion stored; explicit category / priority kept; AI failure → ticket created with defaults; classification saved with the ticket); integration `AiClassificationTests` with the fake AI (agent-created ticket gets the category; email-less portal ticket too; below threshold suggestion only; override recorded through the real endpoints; AI down → 201 and `status` "none"; no key → 201), `TicketsAuthorizationTests` route list.
2. **Implement** domain, service, repository, migration, endpoint, wiring in `TicketService` (+ `ChangePriorityAsync`) and `TicketCategoryChangeService`.

## Frontend Tasks

1. **Tests first:** `api/ai.test.ts`; `features/ai/TicketAiClassification.test.tsx` (applied text, suggestion-only text with Apply button calling the category and priority APIs, override note, nothing when status "none", Apply hidden without `tickets.manage`).
2. Implement the component, wire it into the ticket page, i18n.

## Edge cases

- The AI call happens inside ticket creation, before the save, with a 10 s budget; a slow provider delays creation by at most that long (documented trade-off; the ticket is never lost).
- Deactivated categories are never offered to the AI. If the suggested category was deactivated later the suggestion still shows its name.
- Auto-assignment rules run after classification, so they see the AI category.

## Out of scope

Re-classifying existing tickets, training on overrides, per-category thresholds.

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test; migration `AddTicketAiClassifications`; `dotnet ef migrations has-pending-model-changes` clean.

## As built

- As planned. Migration `AddTicketAiClassifications`; route `GET /api/tickets/{id}/ai-classification` added to `TicketsAuthorizationTests`. Integration tests that read the fake AI requests use `.Last()` because ticket creation now also calls the AI.
