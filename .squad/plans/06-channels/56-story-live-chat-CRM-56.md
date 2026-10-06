# Story 56 — Live chat (SignalR) (Story: CRM-56)

## Prerequisites

- Story 55 ([55-story-web-forms-CRM-55.md](55-story-web-forms-CRM-55.md)): `IWebFormService` (the offline form reuses it), `IRateLimiter`, `ICaptchaVerifier`.
- SignalR precedent: CRM-28 `Crm.Api/Notifications/NotificationsHub.cs` (hub path `/hubs/...`, JWT in the `access_token` query string: `AuthenticationExtensions.ReadHubTokenFromQuery` accepts it for `/hubs` paths) and `tests/Crm.Api.IntegrationTests/Notifications/NotificationHubTests.cs` (HubConnection over the test server, long polling).
- Tickets: `ITicketService.CreateForCustomerAsync`; customers: `ICustomerService.LookupAsync` / `CreateAsync`.
- New permission `chat.handle` (Agent, Supervisor, Admin, SuperAdmin) — `Permissions.cs`, `RolePermissions.cs`, `client/src/auth/permissions.ts`.
- New migration `AddChat` (tables `ChatSessions`, `ChatMessages`); `TicketChannel.Chat = 6` is stored as a string, no schema change.

---

## Story Goal

1. A visitor starts a chat from the widget (`POST /api/public/chat/sessions` with name + email, optional first message); an agent who is online (connected to `/hubs/chat` with `chat.handle`) gets `ChatStarted` in real time and can `Accept` it (AC 1). No online agent → 409 on start.
2. Messages go both ways through the hub (`Send`) and are stored; every message is pushed to the other side in well under a second (AC 2; the integration test measures < 1 s).
3. `End` (by either side) closes the chat and saves the transcript as a ticket (`TicketChannel.Chat`, customer matched / created by email; subject "Chat with <name>", description = transcript) (AC 3). Ending twice creates one ticket.
4. No agent online → `GET /api/public/chat/availability` says `available: false` and the widget shows the offline form, which `POST /api/public/chat/offline` turns into a ticket (channel Chat, same rules as the web form: validation, captcha, rate limit, confirmation email) (AC 4).
5. Visitor connections are authorised by the session id + secret visitor token returned at start (stored hashed); agents by JWT + `chat.handle`.
6. Agent console `/chat` and the public widget page `/embed/chat`.

---

## Context — Read These Files First

1. `server/src/Crm.Api/Notifications/NotificationsHub.cs`, `server/src/Crm.Api/Auth/AuthenticationExtensions.cs` (hub token from query), `server/src/Crm.Application/Notifications/INotificationPublisher.cs`.
2. `server/src/Crm.Application/WebForms/WebFormService.cs` (offline form), `Crm.Application/Tickets/TicketContracts.cs`.
3. `server/src/Crm.Infrastructure/Persistence/Configurations/` (entity configuration pattern), `CrmDbContext.cs` (DbSets), `Crm.Infrastructure/Portal/` (repository + DI extension pattern).
4. Client: `src/api/notifications-hub.ts` (SignalR connection pattern), `src/app/AppRoutes.tsx`, `src/pages/public/ContactFormPage.tsx`.

---

## Backend Tasks

### 1 — Unit tests first (Red)

`server/tests/Crm.UnitTests/Chat/`:
- `ChatSessionTests` (domain) — `Start` is Waiting; `Accept` → Active with the agent (only from Waiting); `End` → Ended (not twice); `AddMessage` only while Waiting / Active, trims, limits length.
- `ChatServiceTests` (fakes: repository, notifier, presence, customers, tickets) — `Start_WithoutAnAgentOnline_Throws409` (AC 4), `Start_NotifiesTheAgents` (AC 1), `Send_StoresAndPushesToTheOtherSide` (AC 2), `Send_ByAVisitorWithAWrongToken_IsRefused`, `End_SavesTheTranscriptAsAChatTicket` (AC 3), `End_Twice_CreatesOneTicket`, `Accept_AnAlreadyAcceptedChat_Throws409`, `Start_BeyondTheRateLimit_Throws429`.
- `InMemoryAgentPresenceTests` — connect / disconnect, two connections of one agent, `HasOnlineAgents`.
- `WebFormServiceTests` (extend) — the offline form with channel Chat creates a `TicketChannel.Chat` ticket.

### 2 — Domain / Application (Green)

- `Crm.Domain/Chat/`: `ChatSession`, `ChatMessage`, `ChatStatus`, `ChatSender`. `TicketChannel.Chat = 6` (+ dispatcher: Chat tickets reply by email; reports enumerate it automatically).
- `Crm.Application/Chat/`: `IChatService` / `ChatService`, `IChatSessionRepository`, `IChatNotifier`, `IAgentPresence` + `InMemoryAgentPresence` (singleton), `ChatContracts`, `ChatText` (en/ar), request validators. `AddChat()` in `AddApplication`.
- `IWebFormService.SubmitAsync(..., TicketChannel channel = WebForm)` so the offline form is the same pipeline.
- Permission `chat.handle`.

### 3 — Infrastructure (Green)

- EF configurations + `ChatSessionRepository`; `AddChatInfrastructure()`; migration `AddChat` (`dotnet ef migrations add AddChat -p src/Crm.Infrastructure -s src/Crm.Api`).

### 4 — Api

- `Crm.Api/Chat/ChatHub.cs` at `/hubs/chat`: agent (JWT with `chat.handle`) joins group `agents`; visitor (query `session` + `token`) joins group `chat-<id>`; others are aborted. Methods `Accept(sessionId)`, `Send(sessionId, body)`, `End(sessionId)`; server → client events `ChatStarted`, `ChatAccepted`, `MessageReceived`, `ChatEnded`. `SignalRChatNotifier : IChatNotifier`.
- `Crm.Api/Endpoints/ChatEndpoints.cs`: public `GET /api/public/chat/availability`, `POST /api/public/chat/sessions`, `POST /api/public/chat/offline`; staff (`chat.handle`) `GET /api/chat-sessions?status=waiting|active`, `GET /api/chat-sessions/{id}/messages`.

### 5 — Integration tests

`server/tests/Crm.Api.IntegrationTests/Chat/`:
- `ChatHubTests` — visitor start + agent receives `ChatStarted` (AC 1); a visitor message reaches the agent and the reply reaches the visitor, each under 1 s (AC 2); ending creates a ticket with channel chat containing the messages (AC 3); a visitor connection with a wrong token is refused; a staff user without `chat.handle` is not an agent.
- `ChatEndpointsTests` — start without agents → 409, availability false/true, offline form → 201 + ticket channel `chat` (AC 4), anonymous / permission checks on the staff endpoints.

---

## Frontend Tasks

- Tests first: `api/chat.test.ts`, `pages/chat/ChatConsolePage.test.tsx` (waiting list, accept, send, end; hub is mocked), `pages/public/ChatWidgetPage.test.tsx` (start form, offline fallback form when unavailable, message sending).
- `src/api/chat.ts` (REST + `connectChatHub(...)` typed events), `src/features/chat/` (`useChatSession`, `ChatTranscript`), `src/pages/chat/ChatConsolePage.tsx` (route `/chat`, `chat.handle`, nav item), `src/pages/public/ChatWidgetPage.tsx` (route `/embed/chat`), i18n `chat.*` + `nav.chat`, `permissions.chatHandle`.

---

## Edge Cases & Failure Modes

- **Agent online check** is in memory (per server instance); a restart empties it until agents reconnect (the console reconnects automatically).
- **Visitor token** is shown once at start, stored as SHA-256; comparing in constant time. Lost token = chat cannot be resumed.
- **Abandoned chats** (visitor closes the tab) stay Waiting / Active until an agent ends them; automatic timeouts are out of scope.
- **Messages after End** → refused (409 as `HubException`).
- **Long transcripts** are cut at the ticket description limit (10 000 characters), oldest lines kept.
- **Rate limit** of chat starts per IP shares `IRateLimiter` (key `chat:<ip>`); hub calls are limited by message length (2 000 characters) only.

---

## Test Plan

Unit (domain, service, presence), integration (hub, endpoints), client tests. `dotnet test`, `npm test`.

## Verification Steps

1. `server/`: `dotnet build` (0 warnings), `dotnet test`, `dotnet ef migrations has-pending-model-changes`. 2. `client/`: `npm test`, `npm run build`, `npm run lint`.

## Done Criteria

- [ ] AC 1 real-time start. - [ ] AC 2 messages < 1 s. - [ ] AC 3 transcript ticket. - [ ] AC 4 offline form. - [ ] All builds and tests green.
