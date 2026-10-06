# customer-management — plan overview

Entry point for the **customer-management** feature (customer profiles, contact details, interaction timeline, notes and attachments). Stories execute in order by their `NN` prefix; `NN` continues the global sequence after [security-admin](../security-admin/00-overview.md) (06–07).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 08 | [08-story-customer-profiles.md](08-story-customer-profiles.md) | Customer profiles (CRUD) | CRM-8 | 01–05 (foundation), 06, 07 |

## Dependency notes

- **Every story here builds on the foundation feature** ([../foundation/00-overview.md](../foundation/00-overview.md)): ProblemDetails + Application exceptions (CRM-5), `CrmDbContext` / migrations / `TimeProvider` / `CrmApiFactory` (CRM-2), shadcn layout + routes + React Query (CRM-3), ar/en i18n + `<Feature>Text` server texts (CRM-4) — and on **security-admin** ([../security-admin/00-overview.md](../security-admin/00-overview.md)): `PagedResult<T>` + `PagingDefaults`, the users page as UI precedent (CRM-6), the permissions `customers.view` / `customers.manage` (every seeded role has both), `RequireAuthorization(Permissions.X)`, the guards in `PermissionPolicyTests`, client `<Can>` / `RequirePermission` (CRM-7).
- **Story 08 (CRM-8)** adds the first Domain entity and the shared pieces later stories reuse: `Crm.Domain.Customers.Customer` (primary `Email` / `Phone` columns, UTC timestamps passed in, soft delete), `Crm.Domain.Common.ISoftDeletable`, the named query filter `CrmDbContext.SoftDeleteFilter` + guard `EverySoftDeletableEntity_HasTheSoftDeleteQueryFilter`, the UTC `DateTime` convention (`UtcDateTimeConverter`), `IEntityTypeConfiguration<T>` classes in `Crm.Infrastructure/Persistence/Configurations/`, the Application-service + Infrastructure-repository pattern (`CustomerService` / `ICustomerRepository` / `CustomerRepository`), `LikePattern`, `PagingText`, migration `AddCustomers`, `/api/customers` endpoints; client `client/src/api/paging.ts` (`PagedResult`, `ListParams`, `listPath`), `apiDelete`, `client/src/api/customers.ts`, the Customers page in `client/src/pages/customers/` + `client/src/features/customers/`.
- **CRM-9 (contact details)** adds `CustomerContact` rows (many phones/emails/WhatsApp, E.164, one primary per type) in a new migration that copies `Customers.Email` / `Customers.Phone` into it as primary contacts; the two columns stay as the denormalized primary values, so the CRM-8 list, search and page keep working. It replaces the loose phone rule with E.164 normalization. Details: section "6 — How later stories build on this" in [08-story-customer-profiles.md](08-story-customer-profiles.md).
- **CRM-10 / CRM-11** hang child entities (FK `CustomerId`, `Restrict`) and routes `/api/customers/{id}/…` off the customer; a deleted customer answers 404. **Tickets (CRM-13)** reference `CustomerId` with `Restrict` and must read customers with `IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])` so tickets of soft-deleted customers stay visible (CRM-8 AC 5).
