# Story 33 — Team collaboration: internal notes & @mentions (Story: CRM-33)

## Prerequisites

- CRM-15 internal notes (`TicketMessageService`, `AddTicketMessageRequest.Internal`, `ListCustomerVisibleAsync` excludes notes), [../05-sla-automation/28-story-alerts-notifications-CRM-28.md](../05-sla-automation/28-story-alerts-notifications-CRM-28.md) (`INotificationDispatcher`, `NotificationType.Mention`, bell link to the ticket, client message `notifications.types.mention`).
- No new permission, no migration.

---

## Story Goal

1. `AddTicketMessageRequest` gets `MentionedUserIds` (optional list of user ids). For an **internal note** every mentioned **active** user (except the author) gets a `Mention` notification (AC 1): ticket id (the bell opens the ticket, AC 2), text = excerpt (first 100 characters) of the note, key `mention:{messageId}` (one per user and note).
2. **Inactive or unknown** users get nothing (AC 3, the dispatcher only notifies active staff).
3. **Public replies never notify** and notes are never sent through a channel; `ListCustomerVisibleAsync` still leaves notes out, and mention data is not part of any customer-visible response (AC 4).
4. UI: when "internal note" is ticked, the reply form shows a "Mention a colleague" select (active staff from `/api/tickets/assignees`); choosing one inserts `@Name` into the note and remembers the id; ids whose `@Name` was deleted from the text are not sent.

**Not in scope:** parsing `@name` from free text, role / group mentions, storing mentions.

---

## Context — Read These Files First

1. `CLAUDE.md`; intake `.squad/stories/07-agent-dashboard/CRM-33/intake.md`.
2. `server/src/Crm.Application/Tickets/TicketMessageService.cs`, `TicketMessageContracts.cs`, `AddTicketMessageRequestValidator.cs`, `Crm.Application/Notifications/NotificationContracts.cs`.
3. `server/tests/Crm.UnitTests/Tickets/TicketMessageServiceTests.cs`, `Crm.Api.IntegrationTests/Tickets/TicketMessagesTests.cs`, `Notifications/NotificationsApiTests.cs`.
4. Client: `features/tickets/TicketReplyForm.tsx`, `api/tickets.ts` (`addTicketMessage`), `features/tickets/useTickets.ts` (`useTicketAssignees`).

---

## Backend Tasks

### 1 — Tests first (Red)

- `Crm.UnitTests/Notifications/MentionTests.cs`: internal note with mentions → one `Mention` request with the users, ticket and excerpt (AC 1, 2); author mentioning themselves → nobody; public reply with mentions → no request (AC 4); no mentions → no request; `ListCustomerVisibleAsync` has no notes (AC 4).
- `Crm.Api.IntegrationTests/Tickets/MentionTests.cs`: agent A writes a note mentioning B and an inactive user C → B's `GET /api/notifications` shows `mention` with the ticket id, C has none (AC 1–3); a public reply mentioning B creates nothing (AC 4).

### 2 — Application

- `AddTicketMessageRequest(..., IReadOnlyList<Guid>? MentionedUserIds = null)`; `TicketMessageService` takes an optional `INotificationDispatcher` and notifies after the note is saved (swallowing dispatcher failures is the dispatcher's job).

---

## Frontend Tasks

- Tests first: `TicketDetailsPage.test.tsx` (mention select only for notes; sends `mentionedUserIds`; drops removed mentions), `api/tickets.test.ts`.
- `api/tickets.ts` (`mentionedUserIds`), `TicketReplyForm` mention select, i18n `tickets.details.mention*` (en + ar).

---

## Edge Cases & Failure Modes

- Same user listed twice: one notification (dispatcher de-duplicates ids and keys).
- Note saved, dispatcher fails: the note stays, the dispatcher swallows push / mail errors; a store failure is a 500 after the save (the note exists).
- Mentioned user is the assignee: still notified once (type Mention).
- Closed ticket: notes are refused (existing rule), so nobody is notified.

## Test Plan

Unit (mentions), integration (API), client (reply form).

## Verification Steps

1. **Backend builds:** `cd server && dotnet build && dotnet test`.
2. **Frontend runs:** `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1: a mention in a note notifies the user.
- [ ] AC 2: the notification opens the ticket.
- [ ] AC 3: inactive users get nothing.
- [ ] AC 4: notes and mentions never reach the customer.
- [ ] build / tests / lint green.
