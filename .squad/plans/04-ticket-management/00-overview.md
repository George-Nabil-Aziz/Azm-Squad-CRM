# ticket-management — plan overview

Entry point for the **ticket-management** feature (ticket categories & priorities, create ticket, ticket list & filters, details & replies, assignment, status workflow, history). Stories execute in order by their `NN` prefix; `NN` continues the global sequence after [customer-management](../03-customer-management/00-overview.md) (08–11; 10 and 11 are CRM-10 / CRM-11).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 12 | [12-story-ticket-categories-CRM-12.md](12-story-ticket-categories-CRM-12.md) | Ticket categories & priorities | CRM-12 | 01–05 (foundation), 06, 07 |

## Dependency notes

- **Every story here builds on the foundation feature** ([../01-foundation/00-overview.md](../01-foundation/00-overview.md)) and on **security-admin** ([../02-security-admin/00-overview.md](../02-security-admin/00-overview.md)): ProblemDetails + Application exceptions, `CrmDbContext` / migrations / `TimeProvider` / `CrmApiFactory`, `PagedResult<T>` + `PagingDefaults`, the permission catalogue (`tickets.view`, `tickets.manage`, `tickets.assign`, `categories.manage`), `RequireAuthorization(Permissions.X)`, the `PermissionPolicyTests` guards, client `<Can>` / `RequirePermission`.
- **Customer-management** ([../03-customer-management/00-overview.md](../03-customer-management/00-overview.md)): tickets reference `Customer` (CRM-8) with a `Restrict` FK and read customers with `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])` so tickets of soft-deleted customers stay visible. CRM-10 (interaction timeline) and CRM-11 (notes) are built in parallel on another branch; the ticket-created timeline entry is wired after CRM-10 merges (see the CRM-13 plan).
- **Story 12 (CRM-12)** adds `Crm.Domain.Tickets.TicketPriority` (High / Mid / Low, API names `high` / `mid` / `low` via `TicketValues`), `TicketCategory` (unique `NormalizedName`, `IsActive`), `/api/ticket-categories` (read `tickets.view`, write `categories.manage`), migration `AddTicketCategories`, the "Ticket categories" admin page and client `ticketPriorities`.
