# Story 62 — Multi-branch (Story: CRM-62)

## Prerequisites

- Story 61 completed: the per-request data scope (`IDataScope`, `DataScope`, `DataScopeLoader`, `DataScopeMiddleware`) and the named query filter pattern on `Ticket` (`CrmDbContext.DepartmentFilter`).
- Customers (`Customer`, `CustomerService`, `CustomerRepository`), tickets (`TicketService`, `ChannelTicketService`), users (`UserService`), reports (`Crm.Application/Reports`, `ReportsRepository`, `CsatReadModel`).
- Migration: `AddBranches` (table `Branches`; columns `AspNetUsers.BranchId`, `Customers.BranchId`, `Tickets.BranchId`). New permission: `branches.manage` (SuperAdmin only).

## Story Goal

A SuperAdmin links users, customers and tickets to branches; branch staff only see their branch.

1. **AC 1** — `Branch` entity + `/api/branches` CRUD (`branches.manage`, SuperAdmin; the list is open to every signed-in staff user, a branch-restricted user sees only their branch). `ApplicationUser.BranchId`, `Customer.BranchId`, `Ticket.BranchId` (all nullable = "no branch / head office"). A user's branch is set at creation or with `PUT /api/users/{id}/branch` (`branches.manage` only, so a branch user can never lift their own restriction). A customer's branch is set on create/update (`branchId`); a **new ticket takes its customer's branch**; moving a customer to another branch moves its tickets (`ExecuteUpdate`).
2. **AC 2** — A **branch manager = any staff user (other than SuperAdmin) assigned to a branch**. The data scope gets `RestrictBranch` / `BranchId`; two EF named query filters (`"Branch"` on `Customer` and on `Ticket`) hide every row of another branch and of no branch. Restricted users creating a customer get their own branch (naming another one is 400 `branchId`); they cannot move customers. Cross-branch ids answer **404**.
3. **AC 3** — All reports (`/api/reports/tickets|sla|sla/breaches|csat|agents|dashboard` and the exports) accept `branchId`; the CSAT read model and the dashboard KPI queries honour it.
4. **AC 4** — A SuperAdmin (or any user without a branch) is never restricted and sees every branch; the reports filter by choice only.

**Decisions**

- No `BranchManager` role is added (the four seeded roles stay): the branch assignment, not a role, scopes the data. Documented deviation from the wording "branch manager".
- Rows with no branch are hidden from branch-restricted users (strict), visible to everyone else.
- On `PUT /api/customers/{id}` a missing `branchId` means "unchanged".

## Context — Read These Files First

1. `server/src/Crm.Infrastructure/Persistence/DataScope.cs`, `CrmDbContext.cs` (`DepartmentFilter`, `ScopeRestrictsDepartments`) — extend with the branch.
2. `server/src/Crm.Application/Customers/CustomerService.cs`, `CustomerContracts.cs`, `Crm.Infrastructure/Customers/CustomerRepository.cs`.
3. `server/src/Crm.Application/Tickets/TicketService.cs` (`CreateCoreAsync`), `ChannelTicketService.cs`, `ITicketRepository.cs` (`CustomerExistsAsync`).
4. `server/src/Crm.Application/Reports/*` (`TicketReportQuery`, `SlaQuery`, `SlaBreachesQuery`, `CsatQuery`, `AgentQuery`, filters, `DashboardService`), `Crm.Infrastructure/Reports/ReportsRepository.cs`, `Crm.Infrastructure/Portal/SurveyRepository.cs` (`CsatReadModel`).
5. `server/src/Crm.Infrastructure/Identity/UserService.cs`, `ApplicationUser.cs`, `Crm.Application/Departments/*` and `Crm.Api/Endpoints/DepartmentsEndpoints.cs` (copy the slice for branches).
6. `client/src/features/reports/ReportsLayout.tsx`, `client/src/features/customers/CustomerFormDialog.tsx`, `client/src/features/users/UserFormDialog.tsx`, `client/src/pages/departments/DepartmentsPage.tsx`.

## Backend Tasks

### 1 — Tests first (Red)

See Test Plan.

### 2 — Domain / Application

- `Crm.Domain/Branches/Branch.cs` (like `Department`); `Customer.BranchId` + `ChangeBranch`; `Ticket.BranchId` + `AssignBranch`.
- `Crm.Application/Branches/*`: contracts, `IBranchRepository`, `IBranchService`/`BranchService`, validator, `BranchText`.
- `IDataScope`: add `RestrictBranch`, `BranchId`. `Permissions.BranchesManage` (SuperAdmin only; update the Admin exception list).
- `CustomerRequest.BranchId`, `CustomerResponse.BranchId`; `CustomerService` resolves the branch (restricted creator → own, unrestricted → validated, update → unchanged when null) and moves the tickets (`ICustomerRepository.MoveTicketsToBranchAsync`).
- `ITicketRepository.GetCustomerBranchAsync`; `TicketService` / `ChannelTicketService` stamp `Ticket.BranchId`; `TicketResponse.BranchId`.
- Users: `CreateUserRequest.BranchId`, `UserResponse.BranchId`, `IUserService.SetBranchAsync` + `SetUserBranchRequest`.
- Reports: trailing `Guid? BranchId = null` on the five query records and on `TicketReportFilter`, `SlaFilter`, `CsatFilter`; `IReportsRepository.OpenTicketsAsync/BreachedTodayAsync` take `Guid? branchId`; `IDashboardService.GetAsync(Guid? branchId, ct)`.

### 3 — Infrastructure / Api

- Configurations (`Branches`, FKs Restrict, indexes), `ApplicationUser.BranchId`, `CrmDbContext` (`Branches`, `ScopeRestrictsBranch`, `ScopeBranchId`, filters `"Branch"` on `Customer` and `Ticket`), `DataScope.RestrictToBranch`, `DataScopeLoader` (reads `Users.BranchId` for non-SuperAdmins), `BranchRepository`, report repositories, migration `AddBranches`.
- `BranchesEndpoints.cs`, `PUT /api/users/{id}/branch`, `branchId` on the report endpoints (via the query records), DI.

## Frontend Tasks

- `api/branches.ts`, `auth/permissions.ts` (`branches.manage`), nav item, route, `pages/branches/BranchesPage.tsx` + `features/branches/*` (table, form dialog).
- `branchId` on customers (form select), users (select for `branches.manage`; edit calls `PUT /api/users/{id}/branch`), reports (`ReportsLayout` branch select shared through the outlet context → every report hook).
- i18n en/ar; RTL-safe classes; shadcn only.

## Edge Cases & Failure Modes

- Customer without a branch + branch-restricted user: 404 (strict).
- Ticket created for a customer before the customer got a branch keeps `BranchId = null` until the customer moves (the move updates the tickets).
- A restricted user passing another branch's `branchId` to a report gets empty numbers (the filter intersects), never other data.
- Inbound channel / portal / jobs: no staff user → unrestricted; the ticket still takes the customer's branch.
- SuperAdmin with a branch assigned is not restricted.

## Test Plan

1. Unit: `BranchTests`, `Customer/Ticket` branch methods, `BranchServiceTests`, `CustomerServiceBranchTests` (restricted creator, unrestricted, 400 for another branch, unchanged on null, tickets moved), `TicketServiceBranchTests` (ticket takes the customer's branch), `UserServiceBranch` is integration, report services pass `BranchId`, `RolePermissionsTests` (`branches.manage` SuperAdmin only).
2. Integration: `Branches/BranchesTests` (CRUD, 403 admin), `Branches/BranchScopeTests` (branch user sees own customers/tickets, 404 for another and for no-branch, SuperAdmin sees all, ticket inherits branch, customer move moves tickets, user branch needs `branches.manage`), `Branches/BranchReportTests` (ticket report/SLA/agents/CSAT/dashboard filtered by `branchId`).
3. Client: `api/branches.test.ts`, `BranchesPage.test.tsx`, reports branch select test, customer/user form tests.

## Migration / Rollback

`AddBranches` adds a table and three nullable columns; `Down` drops them. Nothing is backfilled (existing rows have no branch).

## Verification Steps

1. **Backend builds:** `dotnet build` in `server/` (0 warnings). **Backend tests:** `dotnet test`.
2. **Frontend:** `npm test`, `npm run build`, `npm run lint` in `client/`.
3. **Regression:** `dotnet ef migrations has-pending-model-changes` (project `src/Crm.Infrastructure`, startup `src/Crm.Api`).

## Done Criteria

- [ ] AC 1 branches linked to users, customers, tickets.
- [ ] AC 2 branch users see only their branch (404 elsewhere).
- [ ] AC 3 every report filters by branch.
- [ ] AC 4 SuperAdmin sees all branches.
- [ ] `dotnet test`, `npm test`, build, lint green.
