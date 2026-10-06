# customer-portal — plan overview

Entry point for the **customer-portal** feature (self-service for customers under `/portal`: sign in with an emailed one-time code, submit and track tickets, browse the knowledge base, rate support). Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 40 | [40-story-portal-login-CRM-40.md](40-story-portal-login-CRM-40.md) | Customer portal: login | CRM-40 | 01–11, 23 |
| 41 | [41-story-portal-submit-ticket-CRM-41.md](41-story-portal-submit-ticket-CRM-41.md) | Customer portal: submit ticket | CRM-41 | 40, 13, 20 |
| 42 | [42-story-portal-track-requests-CRM-42.md](42-story-portal-track-requests-CRM-42.md) | Customer portal: track requests & history | CRM-42 | 40, 41, 15, 17 |
| 43 | [43-story-portal-faqs-CRM-43.md](43-story-portal-faqs-CRM-43.md) | Customer portal: access FAQs | CRM-43 | 36–38 |
| 44 | [44-story-portal-csat-CRM-44.md](44-story-portal-csat-CRM-44.md) | Customer portal: submit feedback (CSAT) | CRM-44 | 40–42, 17 |

## Dependency notes

- **Customers are not staff users.** A portal token is a normal JWT of the same issuer with role `Customer` (no permissions in `RolePermissions`) and `sub` = customer id. Staff APIs therefore answer **403** to it; portal endpoints use the policy `Portal` (`RequireRole(Customer)`), so a staff token gets 403 there. The "user is still active" check of the JWT bearer handler checks that the customer still exists for `Customer` tokens.
- **Portal API** lives under `/api/portal/*` (`PortalAuthEndpoints`, `PortalTicketsEndpoints`, `PortalKbEndpoints`, `PortalSurveyEndpoints`). Anonymous: code request / verify, knowledge-base reads, survey link. Signed-in customer: everything about tickets. Customers only ever see their own tickets (404 otherwise) and never internal notes.
- **Tickets from the portal** use channel `Portal` and go through `TicketService` (numbering, SLA timers, customer timeline). Emails (login code, confirmation, survey) are sent through `IChannelSender` (Email provider, logged + retried).
- **Settings:** `Portal:BaseUrl` (links in emails, default empty = relative links), `Portal:ReopenWindowDays` (default 7), `Portal:SurveyValidDays` (default 7).
- **Client:** portal pages in `client/src/pages/portal/`, own layout, own session (`client/src/auth/portal-session.ts`) kept apart from the staff session; same i18n (`portal.*`) and RTL support.
