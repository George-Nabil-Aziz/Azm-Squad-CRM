# Story intake

- Folder: `.squad/stories/04-ticket-management/CRM-15/intake.md`

---

## Feature

- **Feature name (display):** Ticket management
- **Feature slug (folder under `plans/`):** `04-ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-15`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

---

## Title

```
Ticket details & replies
```

---

## Description

```
As an agent, I want to open a ticket, see its conversation, reply to the customer, and add internal notes, so that I can handle the request in one screen.
```

---

## Acceptance criteria

```
1. A reply appears in the ticket thread with author and time.
2. An internal note is flagged internal and is never visible to the customer.
3. The first agent reply sets FirstResponseAt (used by SLA).
4. Replying to a Closed ticket returns 400.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1..14 on `main` (tickets CRM-12/13/14, timeline CRM-10 `IInteractionRecorder`).
- **Used by:** CRM-16..18 (same details page), SLA stories (`FirstResponseAt`), channel stories CRM-23..26 (implement `ITicketReplyDispatcher`, write inbound messages).

## Extra notes (optional)

- Shared contract with parallel groups: `Ticket.FirstResponseAt` is a nullable UTC `DateTime?` (exact name). `TicketMessage` has direction (Inbound / Outbound / InternalNote), nullable author user id, body, channel, created UTC, optional delivery status (Pending / Sent / Failed), optional external message id. After saving an outbound reply the service calls `ITicketReplyDispatcher` (no-op now). A timeline entry (`InteractionType.Message`) is recorded through `IInteractionRecorder`.
- Tests first (TDD).

## Technical hints (optional)

- Reference spec (read-only, older prototype): `D:/AzmSquad/9014 CRM/CRM 01/specs/16-unified-multichannel-thread/` (spec.md, contracts/api.md: `GET /api/tickets/{id}/messages`, chronological list tagged with channel) and `specs/21-team-collaboration/` (internal notes). Notion acceptance criteria and this repo's CLAUDE.md win where they differ.
- Page `tickets/:id`: ticket details, thread, reply box with an "Internal note" toggle.

## Out of scope

- Real delivery through email / WhatsApp (channel stories), attachments on messages, assigning (CRM-16), status changes (CRM-17), history tab (CRM-18), customer portal view.
