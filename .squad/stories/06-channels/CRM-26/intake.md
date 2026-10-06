# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/06-channels/CRM-26/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Channels
- **Feature slug (folder under `plans/`):** `06-channels`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-26` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
WhatsApp: incoming message creates ticket
```

---

## Description

```
As an agent, I want incoming WhatsApp messages to become tickets automatically, so that WhatsApp requests are tracked like any other ticket.
```

---

## Acceptance criteria

```
1. The webhook verification GET (hub.challenge) returns the challenge when the verify token matches.
2. A webhook with an invalid signature returns 401.
3. A message from a known number is added to that customer's open ticket, or creates a new ticket if none is open.
4. A message from an unknown number creates a new customer and a ticket.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-13 / CRM-15 (tickets, ticket messages) — wiring in Phase 2; CRM-24 (shared inbound pipeline, same branch).
- **Depends on code areas or other stories:** CRM-9 `ICustomerService.LookupAsync` / `CreateAsync` / `AddContactAsync` (WhatsApp contact); CRM-7 (webhooks are anonymous + signature-checked); CRM-10 `IInteractionRecorder`.

## Extra notes (optional)

- Phase 1 = `GET /api/webhooks/whatsapp` (hub.challenge) and `POST` with `X-Hub-Signature-256` HMAC check (→ 401), payload parsing, de-duplication, sender → customer matching / creation. Phase 2 = append to the open ticket or create one once CRM-15 is on `main`.

## Technical hints (optional)

- Signature = HMAC-SHA256 of the raw request body with the app secret, header `sha256=<hex>`, compared in constant time.

## Out of scope

- Media messages (images, audio, documents), location / contacts messages, group chats.
