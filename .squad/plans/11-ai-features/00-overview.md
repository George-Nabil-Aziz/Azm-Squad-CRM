# 11-ai-features — plan overview

Entry point for the **ai-features** feature (Phase 3): AI help for agents (summary, suggested reply, categorization, suggested articles) and a customer chatbot. Stories execute in order by their `NN` prefix; `NN` is the story number.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 50 | [50-story-ai-ticket-summaries-CRM-50.md](50-story-ai-ticket-summaries-CRM-50.md) | AI: ticket summaries | CRM-50 | 13–15 |
| 51 | [51-story-ai-suggested-replies-CRM-51.md](51-story-ai-suggested-replies-CRM-51.md) | AI: suggested replies | CRM-51 | 50, 36–39 |
| 52 | [52-story-ai-automatic-categorization-CRM-52.md](52-story-ai-automatic-categorization-CRM-52.md) | AI: automatic categorization | CRM-52 | 50, 12, 16–17, 27 |
| 53 | [53-story-ai-suggested-solutions-CRM-53.md](53-story-ai-suggested-solutions-CRM-53.md) | AI: suggested solutions | CRM-53 | 50, 51, 39 |
| 54 | [54-story-ai-chatbot-CRM-54.md](54-story-ai-chatbot-CRM-54.md) | AI chatbot | CRM-54 | 50, 51, 40–43 |

## Dependency notes

- **Provider abstraction (story 50):** `IAiTextService` in `Crm.Application/Ai/`; the Infrastructure implementation `AnthropicTextService` calls the Claude Messages API over `HttpClient`. Configuration `Ai:ApiKey` (user-secrets / environment only, never committed), `Ai:Model` (default `claude-haiku-4-5-20251001`), `Ai:ConfidenceThreshold` (default 0.8), `Ai:TimeoutSeconds`. Tests register a `FakeAiTextService`.
- **No key = no crash:** startup never needs the key. AI endpoints answer **503** ProblemDetails ("AI not configured") and `GET /api/ai/status` (staff) / `GET /api/portal/chatbot/status` (portal) report `enabled: false`, so the UI hides the AI actions. A failing provider answers **502** and never changes tickets.
- **Privacy:** prompts are built from masked text (`PiiMasker`: emails and phone numbers) and never contain customer names; prompts and answers are never logged.
- **Knowledge base retrieval (story 51):** `IKbRetriever` (Application) finds published articles matching any word of a text and scores them; reused by 53 and 54. Only published, non-deleted articles are ever used.
- **Permissions:** no new permission. Summary / reply draft / suggestions use `tickets.view` (read) and `tickets.manage` (generate), suggestions also `kb.view`; the chatbot is a portal feature (anonymous chat, signed-in customers for hand-off).
- **Migrations (on top of main's snapshot):** `AddTicketAiSummaries` (50), `AddTicketAiClassifications` (52), `AddTicketSuggestionFeedback` (53).
