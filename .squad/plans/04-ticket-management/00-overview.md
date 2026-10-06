# ticket-management — plan overview

Entry point for the **ticket-management** feature (ticket categories & priorities, create ticket, ticket list & filters, details & replies, assignment, status workflow, history). Stories execute in order by their `NN` prefix; `NN` continues the global sequence after [customer-management](../03-customer-management/00-overview.md) (08–11; 10 and 11 are CRM-10 / CRM-11).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 12 | [12-story-ticket-categories-CRM-12.md](12-story-ticket-categories-CRM-12.md) | Ticket categories & priorities | CRM-12 | 01–05 (foundation), 06, 07 |
| 13 | [13-story-create-ticket-CRM-13.md](13-story-create-ticket-CRM-13.md) | Create ticket | CRM-13 | 01–05, 06, 07, 08, 12, 10 (timeline entry) |
| 14 | [14-story-ticket-list-CRM-14.md](14-story-ticket-list-CRM-14.md) | Ticket list & filters | CRM-14 | 01–05, 06, 07, 08, 12, 13 |
| 15 | [15-story-ticket-details-replies-CRM-15.md](15-story-ticket-details-replies-CRM-15.md) | Ticket details & replies | CRM-15 | 01–05, 06, 07, 08, 10, 12, 13, 14 |
| 16 | [16-story-assign-ticket-CRM-16.md](16-story-assign-ticket-CRM-16.md) | Assign ticket to agent | CRM-16 | 01–05, 06, 07, 12, 13, 14, 15 |
| 17 | [17-story-ticket-status-workflow-CRM-17.md](17-story-ticket-status-workflow-CRM-17.md) | Ticket status workflow | CRM-17 | 01–05, 06, 07, 12, 13, 15, 16 |
| 18 | [18-story-ticket-history-CRM-18.md](18-story-ticket-history-CRM-18.md) | Ticket history (audit trail) | CRM-18 | 01–05, 06, 07, 12, 13, 16, 17, 22 |

## Dependency notes

- **Every story here builds on the foundation feature** ([../01-foundation/00-overview.md](../01-foundation/00-overview.md)) and on **security-admin** ([../02-security-admin/00-overview.md](../02-security-admin/00-overview.md)): ProblemDetails + Application exceptions, `CrmDbContext` / migrations / `TimeProvider` / `CrmApiFactory`, `PagedResult<T>` + `PagingDefaults`, the permission catalogue (`tickets.view`, `tickets.manage`, `tickets.assign`, `categories.manage`), `RequireAuthorization(Permissions.X)`, the `PermissionPolicyTests` guards, client `<Can>` / `RequirePermission`.
- **Customer-management** ([../03-customer-management/00-overview.md](../03-customer-management/00-overview.md)): tickets reference `Customer` (CRM-8) with a `Restrict` FK and read customers with `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])` so tickets of soft-deleted customers stay visible. CRM-10 (interaction timeline) and CRM-11 (notes & attachments) were built in parallel and merged into this branch before CRM-13 finished; CRM-13 records the `ticketCreated` timeline entry through `IInteractionRecorder`.
- **Story 12 (CRM-12)** adds `Crm.Domain.Tickets.TicketPriority` (High / Mid / Low, API names `high` / `mid` / `low` via `TicketValues`), `TicketCategory` (unique `NormalizedName`, `IsActive`), `/api/ticket-categories` (read `tickets.view`, write `categories.manage`), migration `AddTicketCategories`, the "Ticket categories" admin page and client `ticketPriorities`.
- **Story 13 (CRM-13)** adds the `Ticket` entity (`Number` → `TKT-000001`, unique + sequential via `MAX + 1` under a per-instance lock plus a unique index (409 on a cross-instance collision); `TicketStatus`, `TicketPriority`, `TicketChannel`; `CustomerId`, `CategoryId?`, `AssigneeId?`, `CreatedById?`; UTC times), `TicketView` read model (customer name ignoring the soft-delete filter), `POST /api/tickets` + `GET /api/tickets/{id}`, migration `AddTickets`, the Tickets page with the "New ticket" dialog and the shadcn `textarea`. It records the `ticketCreated` customer timeline entry (CRM-10 AC 2).
- **Story 14 (CRM-14)** adds `GET /api/tickets` (filters status / priority / category / assignee or unassigned / created date range, search by number or subject, paged newest first; `TicketListFilter` applied on `TicketRepository.Rows()`), `GET /api/tickets/assignees` (active staff for pickers), and the Tickets page filter bar + paged table. No migration.
- **Story 15 (CRM-15)** adds `TicketMessage` (Inbound / Outbound / InternalNote), `Ticket.FirstResponseAt`, `GET|POST /api/tickets/{id}/messages`, `ITicketReplyDispatcher` (no-op until the channel stories) and the details page `tickets/:id`; migration `AddTicketMessages`.
- **Story 16 (CRM-16)** adds `POST /api/tickets/{id}/assign` (`tickets.assign` for anyone, an agent may only take / release their own ticket), `Ticket.AssignTo`, and the shared history storage `TicketHistoryEntry` + `ITicketHistoryRecorder` (migration `AddTicketHistory`); CRM-17 / 18 / SLA write history through the recorder.
- **Story 17 (CRM-17)** adds the status flow (`TicketStatusRules`: New → Open → Pending ⇄ Open → Resolved → Closed, reopen to Open), `PUT /api/tickets/{id}/status`, `Ticket.ResolvedAt` (set on Resolved, cleared on reopen), `allowedStatuses` / `resolvedAt` on `TicketResponse`; migration `AddTicketResolvedAt`; status changes are written to the history.
- **Story 18 (CRM-18)** records priority and category changes through `ITicketHistoryRecorder` (new `PUT /api/tickets/{id}/category`), adds the read-only `GET /api/tickets/{id}/history` (history rows plus SLA `Escalated` events, oldest first) and the History tab. No migration.
