# Story 37 — Knowledge base: FAQs (Story: CRM-37)

## Prerequisites

- Story 36 done (KB area, `kb.view` / `kb.manage`, `KbLanguage`, `KbText`).
- CRM 01 reference (read-only): `specs/26-browse-faqs/`.

## Story Goal

1. `kb.manage` users **create, edit and delete** FAQs (question + answer, English and/or Arabic) (AC 1, AC 4).
2. A numeric **display order** controls the order of the list (AC 2); an empty order on create puts the FAQ last.
3. **Only published FAQs** reach readers: the staff list for users without `kb.manage` and the anonymous portal read `GET /api/portal/kb/faqs` (AC 3). The portal UI is CRM-43.
4. Client: FAQs tab in the knowledge base page.

## Context — read these files first

`CLAUDE.md`; plan 36; `server/src/Crm.Application/KnowledgeBase/*`; `client/src/features/knowledge-base/*`.

## Design

- **Domain** `KbFaq` (QuestionEn/AnswerEn, QuestionAr/AnswerAr, `DisplayOrder`, `IsPublished`, soft delete). A language version is a question + an answer (both or neither); at least one version.
- **Application** `IKbFaqService` (`ListAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ListPublishedAsync` for the portal), `IKbFaqRepository`, `KbFaqRequestValidator` (errors on `question` when no question at all, `answerEn` / `questionAr` ... for half versions, `displayOrder` ≥ 0), response records.
- **API** `/api/kb/faqs` (GET `kb.view`; POST / PUT / DELETE `kb.manage`); anonymous `GET /api/portal/kb/faqs` in `PortalKbEndpoints.cs` (published, ordered, localized `{ id, question, answer }`).
- **Migration** `AddKbFaqs`.

## Backend Tasks

1. Tests first: unit `KbFaqTests` (create keeps order / published; half version throws; delete soft), `KbFaqServiceTests` (create without order → last; ordering by `displayOrder`; non-manager list hides unpublished; portal list only published, localized with fallback; validation 400), integration `KbFaqsTests` (CRUD over HTTP, order, agent read-only 403 on write, anonymous portal list returns only published in the right order, Arabic `Accept-Language`).
2. Implement domain, application, repository + configuration + migration, endpoints (`MapKbEndpoints` extended, `MapPortalKbEndpoints`).

## Frontend Tasks

1. Tests first (`KnowledgeBasePage.test.tsx` extended): FAQs tab lists FAQs ordered with their published state; editor creates a FAQ with order; edits; deletes after confirm; agent has no write buttons.
2. `api/knowledge-base.ts` (FAQ functions), `FaqsPanel`, `FaqFormDialog`, schema, hooks, i18n en + ar, tab `faqs` on the page.

## Edge cases

- Same display order twice is allowed (ties are broken by creation time). Negative order → 400. Unpublished FAQ never leaves the staff API for agents or the portal.

## Verification / done

`dotnet build`, `dotnet test`, `npm test`, `npm run build`, `npm run lint` all green. AC 1–4 each covered by a test.

## As built

- Migration `AddKbFaqs` (table `KbFaqs`, index on `(IsPublished, DisplayOrder)`). The anonymous `GET /api/portal/kb/faqs` lives in `PortalKbEndpoints.cs` (CRM-43 extends the file). No deviations.
