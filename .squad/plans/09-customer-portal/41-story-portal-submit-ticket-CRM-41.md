# Story 41 — Customer portal: submit ticket (Story: CRM-41)

## Prerequisites

- Story 40 (portal login, `Portal` policy, `RequirePortalAuth`), 13 (`TicketService`), 20 (SLA timers), 23 (email).
- CRM 01 reference (read-only): `specs/35-submit-tickets-portal/`.

## Story Goal

1. A signed-in customer submits **subject, description, category and attachments** (`POST /api/portal/tickets`, multipart); the ticket gets **channel = Portal**, status New, priority Mid, no staff creator (AC 1).
2. The response carries the **ticket number**; the customer receives a **confirmation email** (subject `Request received [TKT-000123]`, so a reply threads onto the ticket) (AC 2).
3. **Missing (blank) subject returns 400** on `subject` and nothing is stored (AC 3). Bad category → 400 on `categoryId`; bad files → 400 on `files`.
4. **SLA timers start at creation**: the ticket goes through `TicketService` (policy of its priority, `ResponseDueAt` / `ResolutionDueAt`, customer timeline entry) (AC 4).
5. Client: portal "New request" page (`/portal/tickets/new`, signed-in only) with category select and file picker; shows the ticket number afterwards.

## Design

- `ITicketService.CreateForCustomerAsync(customerId, request, TicketChannel)` shares the creation path with `CreateAsync` (a private helper); `Ticket.Create(... createdById: null ...)`.
- **Attachments** (shared by staff): `TicketAttachment` entity (TicketId, FileName, ContentType, Size, StorageKey `tickets/{ticketId:N}/{id:N}`, UploadedByCustomer flag, UploadedAt), `ITicketAttachmentRepository`, `ITicketAttachmentService.AddAsync/ListAsync/DownloadAsync`, validated with the same `AttachmentRules` (10 MB, allowed types, at most 5 files). Staff: `GET /api/tickets/{id}/attachments` and `.../{attachmentId}` (`tickets.view`). Files are saved before the ticket row; on failure they are removed again.
- **Application** `Crm.Application/Portal/PortalTicketService`: `SubmitAsync(customerId, PortalSubmitTicketRequest(Subject, Description, CategoryId, Files))` → `PortalTicketResponse`; sends the confirmation through `IChannelSender` to the customer's primary email (failure to send never fails the submit: the message is logged / retried by the channel layer).
- **API** `PortalTicketsEndpoints.cs` (`Portal` policy): `POST /api/portal/tickets`, `GET /api/portal/ticket-categories` (active categories, names in the request language).
- **Migration** `AddTicketAttachments`.

## Backend Tasks

1. Tests first: unit `PortalTicketServiceTests` (channel Portal, SLA due times set from the policy, no staff creator, confirmation email with the tag, blank subject 400 and nothing saved, unknown / inactive category 400, files validated, too many files 400, timeline entry), integration `PortalSubmitTicketTests` (AC 1–4 over HTTP with multipart; staff sees the ticket with channel `portal` and the attachments; staff token 403; anonymous 401; attachment download by staff returns the bytes).
2. Implement.

## Frontend Tasks

1. Tests first: `PortalNewTicketPage.test.tsx` (fills the form, selects category, attaches a file, submits multipart, shows the number; subject required; server 400 shown; unsigned redirects to sign-in).
2. `api/portal.ts` (`submitPortalTicket`, `listPortalCategories`), `features/portal/PortalTicketForm.tsx`, page + route under `RequirePortalAuth`, i18n.

## Edge cases

- `HttpCurrentUser.UserId` is null for a `Customer` token (customers are not rows of the users table: audit / timeline foreign keys stay valid).
- A customer without an email contact cannot occur (the account is created from the email), the confirmation uses the account email.

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test.
