# Story 61 — Multi-department (Story: CRM-61)

## Prerequisites

- Tickets (`Ticket`, `TicketService`, `TicketRepository`, `TicketHistory*`), SLA (`SlaPolicy`, `ISlaPolicyRepository`), users (`UserService`), permissions (`Permissions.cs`, `RolePermissions.cs`) — all on main.
- Branch `feature/phase3-group-u`. Reference only: CRM 01 specs (departments) for edge cases.
- Migration: `AddDepartments` (tables `Departments`, `UserDepartments`, `DepartmentSlaPolicies`; column `Tickets.DepartmentId`). New permission: `departments.manage` (SuperAdmin, Admin).

## Story Goal

Admins organize tickets and agents by department.

1. **AC 1** — A ticket has at most one department (`Tickets.DepartmentId`, nullable = "general", visible to everyone, for backward compatibility); a user can be in many departments (`UserDepartments`). Admin CRUD `/api/departments` (`departments.manage`; list needs `tickets.view`); users get `departmentIds` on create/update/response.
2. **AC 2** — A user whose roles are only `Agent` is **department-restricted**: they see tickets of their departments plus tickets with no department. Supervisor/Admin/SuperAdmin are not restricted. Enforcement is one EF **named global query filter** (`"Department"`) on `Ticket`, fed by a per-request `IDataScope`, so every path (`GET /api/tickets/{id}`, messages, history, SLA, status, assignment, lists, my-tickets) returns **404** for another department's ticket without touching each service. Jobs, channel inbound and the portal have no signed-in staff user and stay unrestricted.
3. **AC 3** — `PUT /api/tickets/{id}/department` (`tickets.manage`) `{ departmentId }` transfers the ticket (null = general) and writes a history entry (field `Department`, old/new department **names**). Same department = no entry. Unknown/inactive target = 400 `departmentId`. The SLA due times of an existing ticket do **not** move on transfer.
4. **AC 4** — Optional per-department SLA overrides: table `DepartmentSlaPolicies(DepartmentId, Priority)`; `GET/PUT/DELETE /api/departments/{id}/sla-policies[/{priority}]` (`sla.manage`). New tickets (and priority changes) use the department's override for that priority, else the global policy (`ISlaPolicyRepository.FindEffectiveAsync(priority, departmentId)`).

**Decisions**

- `departmentId` on ticket create is optional; a department-restricted creator may only choose one of their own (400 otherwise), and with exactly one membership it is the default.
- Ticket list filter `departmentId`; ticket responses carry `departmentId` / `departmentName`.
- Departments are deactivated, never deleted (like categories); an inactive department cannot be chosen but keeps its tickets.

## Context — Read These Files First

1. `server/src/Crm.Domain/Tickets/Ticket.cs` — add `DepartmentId` + `ChangeDepartment(Guid?, DateTime)` (pattern of `ChangeCategory`).
2. `server/src/Crm.Application/Tickets/TicketService.cs` (`CreateCoreAsync`, `ChangePriorityAsync`) — SLA lookups use `slaPolicies.FindAsync(priority)`; switch to the effective lookup.
3. `server/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` — constructor, `OnModelCreating`; `SoftDeleteFilter` is the named-filter precedent.
4. `server/src/Crm.Infrastructure/Tickets/TicketRepository.cs` — `Rows()` projection (add the department name join), `ListAsync` filters.
5. `server/src/Crm.Application/Tickets/TicketCategoryService.cs` + `TicketCategoryRepository` + `TicketCategoriesEndpoints.cs` + `TicketCategoryConfiguration.cs` — copy this vertical slice for departments.
6. `server/src/Crm.Infrastructure/Identity/UserService.cs`, `Crm.Application/Users/UserContracts.cs` — user create/update/response.
7. `client/src/features/ticket-categories/*`, `client/src/pages/ticket-categories/*`, `client/src/app/navigation.ts`, `AppRoutes.tsx`, `client/src/auth/permissions.ts` — client precedent.

## Backend Tasks

### 1 — Tests first (Red)

See Test Plan. Write them all, run, see them fail.

### 2 — Domain

- Create `Crm.Domain/Departments/Department.cs` (`Create`, `Update(name,isActive)`, `NormalizeName`, like `TicketCategory`), `UserDepartment.cs` (`UserId`, `DepartmentId`), `Crm.Domain/Sla/DepartmentSlaPolicy.cs` (`DepartmentId`, `Priority`, minutes, `UpdatedAt`, `Update`, `ToPolicy()` giving a detached `SlaPolicy`).
- `Ticket.DepartmentId`, `ChangeDepartment`; `TicketHistoryField.Department = 5`.

### 3 — Application

- `Crm.Application/Departments/`: contracts, `IDepartmentRepository`, `IDepartmentService`/`DepartmentService` (list, create, update, SLA overrides list/set/remove with audit), validators, `DepartmentText` (en/ar).
- `Crm.Application/Common/Security/IDataScope.cs` (`RestrictDepartments`, `DepartmentIds`).
- `Crm.Application/Tickets/TicketDepartmentService.cs` (`ITicketDepartmentService.TransferAsync`).
- `ISlaPolicyRepository.FindEffectiveAsync`; `TicketService` / `ChannelTicketService` use it; `CreateTicketRequest.DepartmentId`, `ListTicketsQuery.DepartmentId`, `TicketResponse.DepartmentId/DepartmentName`, `TicketView.DepartmentName` as trailing optional parameters (**backward compatible**).
- Users: `CreateUserRequest/UpdateUserRequest.DepartmentIds` (null on update = unchanged), `UserResponse.DepartmentIds`.
- `Permissions.DepartmentsManage` (Admin gets it through `All`).

### 4 — Infrastructure / Api

- Configurations for the three new entities, `Tickets.DepartmentId` FK (Restrict) + index; `CrmDbContext` sets, ctor `IDataScope? scope = null`, named filter `"Department"` on `Ticket`.
- `DepartmentRepository`; `DataScope` (scoped holder) + `DataScopeLoader` (reads the user's roles/departments); `TicketRepository` department name + filter; `UserService` memberships.
- `Crm.Api/Auth/DataScopeMiddleware.cs` (after `UseAuthentication`), `DepartmentsEndpoints.cs`, `PUT /api/tickets/{id}/department`, DI, migration `AddDepartments`.

## Frontend Tasks

- `api/departments.ts`, `api/tickets.ts` (`departmentId`, transfer), `api/users.ts` (`departmentIds`), `auth/permissions.ts` (`departments.manage`).
- `features/departments/*` (table, form dialog, SLA overrides dialog), `pages/departments/DepartmentsPage.tsx`, route `/departments`, nav item.
- Ticket: department select in `NewTicketDialog`, `TicketDepartmentControl` on the details page (transfer), user form checkboxes.
- i18n `en.json` / `ar.json`; RTL-safe classes, shadcn only.

## Edge Cases & Failure Modes

- Agent with no membership sees only tickets without department (filter: `DepartmentId == null || in list`).
- Agent transfers a ticket out of their departments: the transfer succeeds; the response is built ignoring the scope and the next read is 404.
- Inactive department: cannot be chosen (400); existing tickets keep it.
- Deleting an override falls back to the global policy.
- Scope is loaded once per request in the middleware; a membership change applies from the next request (not in token claims).

## Test Plan

1. Unit: `DepartmentTests`, `DepartmentSlaPolicyTests`, `TicketTests` (`ChangeDepartment`), `DepartmentServiceTests`, `TicketDepartmentServiceTests` (history entry, none when same, 400 unknown), `TicketServiceDepartmentTests` (default department, restricted creator, department SLA used), `RolePermissionsTests` (`departments.manage`).
2. Integration: `Departments/DepartmentsTests` (CRUD, 400 duplicate, 403 agent), `Departments/DepartmentScopeTests` (agent sees own + general, 404 on get/messages/history of another, supervisor sees all, list filtered, transfer then 404), transfer history, `Departments/DepartmentSlaTests` (override used, fallback after delete, 403 for admin), user membership round trip.
3. Client: `api/departments.test.ts`, `DepartmentsPage.test.tsx`, ticket transfer control test, nav/permission tests updated.

## Migration / Rollback

`AddDepartments` only adds tables and a nullable column; `Down` drops them. Existing tickets get `DepartmentId = NULL`.

## Verification Steps

1. **Backend builds:** `dotnet build` in `server/` (0 warnings). **Backend tests:** `dotnet test`.
2. **Frontend:** `npm test`, `npm run build`, `npm run lint` in `client/`.
3. **Regression:** `dotnet ef migrations has-pending-model-changes` (project `src/Crm.Infrastructure`, startup `src/Crm.Api`).

## Done Criteria

- [ ] AC 1 ticket department, user memberships, admin CRUD.
- [ ] AC 2 agent isolation with 404 everywhere.
- [ ] AC 3 transfer recorded in history.
- [ ] AC 4 per-department SLA override.
- [ ] `dotnet test`, `npm test`, build, lint green.

## Deviations (as built)

- Transfer response is `{ ticketId, departmentId, departmentName }` (not the full ticket): a department-restricted agent loses sight of the ticket they just moved.
- The ticket list filter `departmentId` exists in the API and `api/tickets.ts`; no filter control was added to the ticket filter bar (UI shows the department on the details page and in the new-ticket dialog).
- Ticket numbering (`NextNumberAsync`, `NumberTakenAsync`) ignores the query filters: a restricted user sees only some tickets, but numbers must stay unique over all.
