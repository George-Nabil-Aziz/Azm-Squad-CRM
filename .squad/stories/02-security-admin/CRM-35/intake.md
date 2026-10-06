# Story intake

- Folder: `.squad/stories/02-security-admin/CRM-35/intake.md`

---

## Feature

- **Feature name (display):** Security & administration
- **Feature slug (folder under `plans/`):** `02-security-admin`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-35`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
System configuration
```

---

## Description

```
As a SuperAdmin, I want to manage system settings (business hours, time zone, ticket number prefix, email and WhatsApp credentials) from the UI, so that no code change is needed.
```

---

## Acceptance criteria

```
1. Changing business hours affects SLA calculation for new tickets.
2. Secrets are stored encrypted and never returned in API responses.
3. Invalid values return 400.
4. A non-SuperAdmin changing settings gets 403.
```

---

## Attachments

None.

---

## Dependencies

- **Related ids:** CRM-7 (permissions), CRM-13/20 (ticket creation, SLA due times), CRM-19 (SLA policy), CRM-23..26 (email / WhatsApp channels and their options), CRM-34 (audit log, `settings.updated`) — on `main` / earlier in this branch.

## Technical hints (optional)

- CRM 01 reference (read-only, behaviour only): `specs/48-system-configuration/` (spec.md, contracts/api.md, tests/test-cases/48-system-configuration.md). CRM 01 uses `PATCH /api/settings` key/value; this repo uses a typed `GET/PUT /api/settings`.
- New permission `settings.manage` (SuperAdmin only). Secrets (SMTP / IMAP password, WhatsApp access token, app secret, verify token) are protected with ASP.NET Data Protection.
- SLA today runs 24/7 (`SlaPolicy.ResponseDueAt`); business hours make the due time count only minutes inside the configured days / hours / time zone.

## Out of scope

- Per-department / per-branch hours, holidays, rotating the Data Protection keys, a connection-test button for SMTP / WhatsApp, multi-instance live propagation (other instances pick the change up on restart).
