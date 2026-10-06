# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/06-channels/CRM-25/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Channels
- **Feature slug (folder under `plans/`):** `06-channels`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-25` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
WhatsApp: send replies
```

---

## Description

```
As an agent, I want my reply on a WhatsApp ticket to be sent to the customer on WhatsApp (Meta WhatsApp Cloud API), so that customers can be served where they are.
```

---

## Acceptance criteria

```
1. Replying on a WhatsApp ticket sends the message via WhatsApp Cloud API.
2. Outside the 24-hour window, free text is blocked and the agent is told to use an approved template.
3. Delivery status (sent / delivered / read / failed) is updated from the webhook.
4. Sending goes through the IChannelProvider interface.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-13 / CRM-15 (tickets, replies) — wiring in Phase 2; CRM-23 (`IChannelProvider`, outbound message log), CRM-26 (webhook endpoint, signature check) on the same branch.
- **Depends on code areas or other stories:** CRM-9 WhatsApp contacts (E.164); CRM-10 `IInteractionRecorder`.

## Extra notes (optional)

- Phase 1 = Cloud API client (typed `HttpClient`), WhatsApp provider, 24-hour window rule (Domain, unit-tested with a fake `TimeProvider`), status updates from the webhook. Phase 2 = agent reply on a WhatsApp ticket → provider; template choice in the reply UI.

## Technical hints (optional)

- `Channels:WhatsApp:*` settings (`PhoneNumberId`, `AccessToken`, `AppSecret`, `VerifyToken`, `ApiBaseUrl`); tokens only in user-secrets / environment. Tests use a fake `HttpMessageHandler` — no real network calls.

## Out of scope

- Template management UI, media messages, interactive buttons, read receipts sent by the CRM.
