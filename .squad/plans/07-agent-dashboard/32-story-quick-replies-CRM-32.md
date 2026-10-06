# Story 32 — Quick replies with placeholders (Story: CRM-32)

## Prerequisites

- Ticket replies (CRM-15, `TicketReplyForm`), tickets with customers (CRM-13), permissions catalogue (CRM-7).
- New permission **`quick-replies.manage-shared`** (Supervisor, Admin, SuperAdmin). Reading, creating personal ones and rendering need `tickets.manage` (agents). One migration: **`AddQuickReplies`**.

---

## Story Goal

1. **Placeholders** (AC 1): a body may contain `{{customer.name}}`, `{{ticket.number}}`, `{{ticket.subject}}` and `{{agent.name}}` (spaces inside the braces and letter case are ignored). Unknown placeholders stay as typed. Pure rule in the Domain: `QuickReplyTemplate.Render(body, values)`.
2. **Insert** (AC 2): `POST /api/quick-replies/{id}/render` `{ticketId}` returns the text with the ticket's data (customer name, ticket number, subject, signed-in agent's name). The reply form has a "Quick replies" picker (search box + list); choosing one puts the rendered text into the reply box.
3. **Personal or shared** (AC 3): `isShared` false = visible only to the owner; true = visible to everybody. Creating, editing or deleting a **shared** reply needs `quick-replies.manage-shared` → otherwise **403**; somebody else's personal reply is **404**.
4. **Search** (AC 4): `GET /api/quick-replies?search=` lists my personal replies plus all shared ones whose title or shortcut contains the text (case-insensitive), by title.
5. UI: a management page `/quick-replies` (sidebar, `tickets.manage`) with the list, a create / edit form (title, shortcut, body, shared checkbox shown with the permission) and delete; the picker inside `TicketReplyForm`.

**Not in scope:** rich text, attachments, teams, usage counters.

---

## Context — Read These Files First

1. `CLAUDE.md`; intake `.squad/stories/07-agent-dashboard/CRM-32/intake.md`.
2. `server/src/Crm.Application/Tasks/Tasks.cs` (CRM-31 service pattern), `Crm.Application/Tickets/TicketText.cs`, `Crm.Infrastructure/Persistence/LikePattern.cs`, `Crm.Application/Auth/Permissions.cs`, `RolePermissions.cs`.
3. `server/tests/Crm.UnitTests/Auth/RolePermissionsTests.cs`, `Crm.Api.IntegrationTests/Auth/PermissionPolicyTests.cs`.
4. Client: `features/tickets/TicketReplyForm.tsx`, `pages/tickets/TicketDetailsPage.test.tsx`, `pages/tasks/TasksPage.tsx`, `app/navigation.ts`.

---

## Backend Tasks

### 1 — Tests first (Red)

- `Crm.UnitTests/QuickReplies/QuickReplyTemplateTests.cs`: all four placeholders replaced (AC 1, 2), spacing / case tolerant, unknown untouched, no placeholders.
- `Crm.UnitTests/QuickReplies/QuickReplyServiceTests.cs`: personal create, shared create without permission → `ForbiddenException`, edit shared without permission → 403 (AC 3), foreign personal → 404, search by title / shortcut (AC 4), list shows mine + shared only, render uses ticket data, duplicate-free shortcuts not required.
- `Crm.Api.IntegrationTests/QuickReplies/QuickRepliesApiTests.cs`: end-to-end for the above through HTTP (Agent 403 on shared, Supervisor OK, search, render with a real ticket).
- `RolePermissionsTests`, `PermissionPolicyTests` (new permission), `ProtectedEndpointTests`.

### 2 — Domain / Application / Infrastructure / Api

- `Crm.Domain/QuickReplies/QuickReply.cs` + `QuickReplyTemplate`; `Crm.Application/QuickReplies/` contracts, validators, `IQuickReplyRepository`, `IQuickReplyService`, `QuickReplyText`; `Permissions.QuickRepliesManageShared`.
- Infrastructure: `QuickReplyConfiguration` (table `QuickReplies`, index on owner and title), `QuickReplyRepository`; Api: `QuickRepliesEndpoints` (`/api/quick-replies`, `tickets.manage`); migration **`AddQuickReplies`**.

---

## Frontend Tasks

- Tests first: `features/tickets/QuickReplyPicker.test.tsx` (search, choose inserts rendered text), `pages/quick-replies/QuickRepliesPage.test.tsx` (list, create, shared checkbox only with permission, delete, 403 shown), `api/quick-replies.test.ts`.
- `api/quick-replies.ts`, `features/quick-replies/*`, picker in `TicketReplyForm` (+ mock in `TicketDetailsPage.test.tsx`), page, route, sidebar, `permissions.ts`, i18n en + ar.

---

## Edge Cases & Failure Modes

- Customer name containing `{{...}}`: substitution is single pass, never re-expanded.
- Ticket of a deleted customer: the name is still available.
- Search text with `%` / `_`: escaped by `LikePattern`.
- A shared reply edited by its supervisor owner after losing the permission: 403.
- Render for an unknown ticket / reply: 404.

## Test Plan

Unit (template, service), integration (API), client (picker, page, API).

## Migration / Rollback

`AddQuickReplies` creates the table; rollback drops it.

## Verification Steps

1. **Backend builds:** `cd server && dotnet build && dotnet test`; `has-pending-model-changes` says "No changes".
2. **Frontend runs:** `cd client && npm test && npm run build && npm run lint`.

## Done Criteria

- [ ] AC 1: placeholders supported.
- [ ] AC 2: inserting renders the ticket data.
- [ ] AC 3: personal / shared, 403 without permission.
- [ ] AC 4: search by title or shortcut.
- [ ] build / tests / lint green.
