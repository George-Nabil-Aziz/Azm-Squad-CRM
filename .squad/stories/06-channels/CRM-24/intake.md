# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/06-channels/CRM-24/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Channels
- **Feature slug (folder under `plans/`):** `06-channels`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-24` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Email: incoming email creates ticket
```

---

## Description

```
As an agent, I want incoming customer emails to become tickets automatically, so that no request is lost.
```

---

## Acceptance criteria

```
1. An email from a known customer creates a ticket linked to that customer.
2. An email from an unknown sender creates a new customer and a ticket.
3. A reply with [TKT-xxxxxx] in the subject is added to the existing ticket instead of creating a new one.
4. The same email (same Message-Id) processed twice is ignored.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-13 (create ticket) on branch `feature/group-b-tickets`; CRM-15 (ticket messages) built later; CRM-23 (shared channel layer, same branch).
- **Depends on code areas or other stories:** CRM-9 `ICustomerService.LookupAsync` (match the sender), `ICustomerService.CreateAsync` (unknown sender); CRM-10 `IInteractionRecorder` (`messageReceived`).

## Extra notes (optional)

- Phase 1 = IMAP polling + MIME parsing into a channel-neutral inbound message, Message-Id de-duplication, sender → customer matching / creation, ticket-number extraction from the subject. Phase 2 = create the ticket / append the message once CRM-15 is on `main`.

## Technical hints (optional)

- MailKit `ImapClient` (IMAP settings `Channels:Email:Imap:*`, password from user-secrets / environment) behind a wrapper; MimeKit parsing unit-tested with in-memory messages.
- Recurring polling job as a plain class whose method tests call directly; not started in `Testing`.

## Out of scope

- Inbound attachments, HTML rendering, stripping quoted replies, spam filtering, multiple mailboxes.
