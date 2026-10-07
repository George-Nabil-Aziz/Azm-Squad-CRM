# Story 54 — AI chatbot (Story: CRM-54)

## Prerequisites

- Stories [50](50-story-ai-ticket-summaries-CRM-50.md), [51](51-story-ai-suggested-replies-CRM-51.md) (`IAiTextService`, `IKbRetriever`, `PiiMasker`, `AiLanguage`) and the portal (40–43: `PortalRoles.Customer`, `ITicketService.CreateForCustomerAsync`, `PortalLayout`). CRM 01 reference (read-only): `specs/34-ai-chatbot/`.

## Story Goal

A chat page `/portal/chat` backed by `POST /api/portal/chatbot/messages` (anonymous; a signed-in customer's portal token is read when present) and `GET /api/portal/chatbot/status` (`{ enabled }`; the portal hides the chat link when false). The server is stateless: the client sends the transcript so far (`messages: [{ role: "user" | "assistant", content }]`, at most 20 messages of 2 000 characters, last one from the user; otherwise 400 on `messages`) and optional `handoff: true`.

1. **Answers only from published articles, with the article cited** (AC 1): `IKbRetriever` finds up to 4 published articles for the last two customer messages; the AI must answer as JSON `{ canAnswer, answer, articleIds, confidence }` using only them. The reply carries `sources: [{ id, title }]` (cited ids that were retrieved; a cited id that was not retrieved is dropped). An answer without a valid cited article counts as "unknown".
2. **Hand-off creates a ticket with the full transcript** (AC 2): when the customer asks for an agent (keyword check in English and Arabic, no AI call), sends `handoff: true`, or the AI confidence is below `Ai:ConfidenceThreshold`, a ticket (channel Portal) is created for the signed-in customer through `ITicketService.CreateForCustomerAsync`; subject = first customer message, description = the transcript (`Customer: …` / `Chatbot: …`, oldest part cut with a marker above 10 000 characters). Reply `outcome: "handoff"`, `ticket: { id, number }`. A visitor who is not signed in gets `outcome: "handoff"`, `signInRequired: true` and no ticket (anonymous ticket creation is refused on purpose: spam and no customer to attach it to).
3. **Arabic and English** (AC 3): the reply language follows the last customer message (`AiLanguage.Detect`); the fixed messages (unknown, hand-off, sign-in) are written in that language, the page is i18n (ar / en, RTL).
4. **"I do not know" instead of inventing** (AC 4): no matching article, `canAnswer` false or no valid citation → fixed "I could not find this in our help articles" message, `outcome: "unknown"`, `offerAgent: true` (the page shows "Talk to an agent", which sends `handoff: true`). The AI is not called when no article matched.

Without a key the endpoint is **503** and status says disabled; a failing provider is **502** (the page shows the error and the customer can still use the ticket form). Personal data (emails, phones) in customer messages is masked before it goes to the AI; prompts are never logged. Cost / abuse limits: message count and size limits above; hand-off needs sign-in (no per-IP rate limit in this story).

## Design

- Application `Ai/ChatbotService.cs` (`IChatbotService.ReplyAsync(customerId?, request)`, contracts `ChatbotRequest/Message/Reply/Source/Ticket`); endpoints `Crm.Api/Endpoints/PortalChatbotEndpoints.cs`; no migration, no new permission, no storage (the transcript lives in the ticket).
- Client `api/portal-chatbot.ts`, `pages/portal/PortalChatPage.tsx` (+ `features/portal/ChatMessages` inline), route `/portal/chat`, nav link in `PortalLayout` when enabled, i18n `portal.chat.*`.

## Tasks (tests first)

- Backend: unit `ChatbotServiceTests` (published-only retrieval and citation, uncited / foreign ids → unknown, no articles → unknown without an AI call, `canAnswer` false → unknown, low confidence → ticket with transcript, agent keywords en / ar → ticket without AI call, `handoff: true`, anonymous → signInRequired and no ticket, language en / ar for fixed messages, masking, validation of messages, long transcript cut, 502 / 503 propagate, unparsable AI answer → 502); integration `PortalChatbotTests` (status, answer with citation from a published article and not from a draft, hand-off creates a portal ticket visible to the customer with the transcript, anonymous hand-off needs sign-in, 503 without key, 400 on bad input).
- Frontend: `PortalChat.test.tsx` (sends a message and shows the answer with its cited article link, unknown shows "Talk to an agent" which sends `handoff`, ticket created message with number, sign-in required message, disabled state, RTL / Arabic text direction); `api/portal-chatbot` test. Then implement.

## Out of scope

Live chat, stored chat sessions, streaming, rate limiting per IP.

## As built

- As planned: `ChatbotService`, `PortalChatbotEndpoints`, `/portal/chat` page and header link (shown only while the status endpoint says enabled). The hand-off button adds a customer message ("I would like to talk to an agent.") so the transcript always ends with the customer. No migration, no new permission. `PortalApp.Create` got an optional service configuration hook for the tests; the portal sign-in test ignores the new status request.
