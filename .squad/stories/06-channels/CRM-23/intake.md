# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/06-channels/CRM-23/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Channels
- **Feature slug (folder under `plans/`):** `06-channels`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-23` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Email: send replies
```

---

## Description

```
As an agent, I want my reply on an email ticket to be sent to the customer by email, so that the customer gets answers in their inbox.
```

---

## Acceptance criteria

```
1. Replying on an email ticket sends an email to the customer primary email via SMTP (MailKit).
2. The email subject contains the ticket number, e.g. [TKT-000001].
3. On SMTP failure the message is marked Failed and retried.
4. Sending goes through the IChannelProvider interface.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-13 (create ticket, `Ticket`, `TicketChannel.Email`, `Ticket.FormatNumber`) on branch `feature/group-b-tickets`; CRM-15 (ticket details & replies, ticket messages, `FirstResponseAt`) built later by another group.
- **Depends on code areas or other stories:** CRM-9 customer contacts (primary email = `Customer.Email`); CRM-10 `IInteractionRecorder` (`InteractionType.Message`, `messageSent`); CRM-7 permission `channels.manage` (channel settings).

## Extra notes (optional)

- Built in two phases: Phase 1 = the channel layer without tickets (`IChannelProvider`, SMTP sender, outbound message log with Failed + retry); Phase 2 = wiring the agent's ticket reply to the provider once CRM-15 is on `main`.
- Shares the channel abstraction with CRM-24..26 (same branch `feature/group-e-channels`).

## Technical hints (optional)

- MailKit `SmtpClient` behind a small wrapper so tests never open a network connection.
- SMTP settings in `Channels:Email:Smtp:*`; the password only in user-secrets / environment variables. Missing settings must not crash startup (provider reports "not configured").

## Out of scope

- HTML templates / signatures, attachments on outgoing emails, per-agent mailboxes, bounce processing, SLA logic (CRM-19..22).
