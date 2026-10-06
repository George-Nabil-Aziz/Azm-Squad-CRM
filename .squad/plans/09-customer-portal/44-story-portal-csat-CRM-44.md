# Story 44 — Customer portal: submit feedback (CSAT) (Story: CRM-44)

## Prerequisites

- Stories 17 (status workflow), 23 (email), 40–42 (portal), CRM-45..49 on `main` (`ICsatReadModel`, CSAT report).
- CRM 01 reference (read-only): `specs/39-submit-feedback/`.

## Story Goal

1. **When a ticket becomes Resolved the customer gets a survey** (AC 1): `TicketStatusService` calls `ISurveyService.OnTicketResolvedAsync`, which issues a `TicketSurvey` (random token, `ExpiresAt` = issue + `Portal:SurveyValidDays`, default 7) and emails the link `{Portal:BaseUrl}/portal/survey/{token}`. In the portal the ticket page offers the rating while a survey is open. A ticket resolved again after a reopen keeps its answered survey (nothing is sent); an unanswered one is renewed (same token, new expiry) and mailed again.
2. **Rating 1–5 with an optional comment, saved once per ticket** (AC 2): `POST /api/portal/surveys/{token}` (the anonymous email link) or `POST /api/portal/tickets/{id}/feedback` (signed in, own ticket). A second submit → **400**.
3. **A rating outside 1–5 (or missing) → 400** on `rating` (AC 3).
4. **The survey link expires after 7 days** (AC 4): after `ExpiresAt` a submit is 400 on `token` and the survey page says the link expired; an unknown token is 404.
5. **CSAT report** (CRM-45..49 integration): `ICsatReadModel` is implemented over `TicketSurveys` (`CsatSnapshot(Ratings, SurveysSent)` with ticket, stars, comment, time, agent, category) and registered in place of `EmptyCsatReadModel`; a submitted rating shows in `GET /api/reports/csat`.

## Design

- **Domain** `TicketSurvey` (TicketId unique, Token, IssuedAt, ExpiresAt, Rating?, Comment?, RatedAt?); `Issue`, `Renew`, `Rate` → `SurveyRateResult { Ok, InvalidRating, AlreadyRated, Expired }`, `State(now)` → Open | Answered | Expired.
- **Application** `ISurveyService` + `ISurveyRepository`, validators, `SurveyText`; `TicketStatusService` gets an optional `ISurveyService?` (null in older unit tests).
- **Infrastructure** `SurveyRepository`, `CsatReadModel : ICsatReadModel`, configuration, migration `AddTicketSurveys` (on top of main's snapshot).
- **API** `PortalSurveyEndpoints.cs`: anonymous `GET/POST /api/portal/surveys/{token}`; `Portal` policy `GET/POST /api/portal/tickets/{id}/feedback`.
- **Client** `/portal/survey/:token` (public) with star buttons and comment; the portal ticket page shows the same form while a survey is open.

## Backend Tasks

1. Tests first: unit `TicketSurveyTests` (issue expiry = 7 days, rate ok / 0 / 6 / twice / after expiry exactly at 7 days, renew), `SurveyServiceTests` (resolved → survey + email with link, second resolve no second mail when answered, unanswered renewed, rating validation, own ticket check), `TicketStatusService` calls it on Resolved only; integration `PortalSurveyTests` (resolve by staff → mail with link, submit by token OK and visible to staff report, second 400, rating 0 / 6 / missing 400, expiry with fake clock, unknown token 404, signed-in feedback on own ticket, other customer's ticket 404, **rating appears in `GET /api/reports/csat`**).
2. Implement.

## Frontend Tasks

1. Tests first: survey page by token (stars, comment, thanks, already answered / expired messages, 400 shown); portal ticket page shows the form for a resolved ticket with an open survey.
2. `api/portal.ts` additions, `PortalSurveyPage`, `RatingForm`, `PortalFeedback` section, i18n.

## Edge cases

- The anonymous link is the credential (random 32 chars, unguessable); it works only for this one survey. A deleted customer has no email: nothing is sent, the portal form still works.
- Survey count for the report ("surveys sent") = surveys issued in the range.

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test; migration `AddTicketSurveys`.
