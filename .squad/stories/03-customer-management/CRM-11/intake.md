# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/03-customer-management/CRM-11/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Customer management
- **Feature slug (folder under `plans/`):** `03-customer-management`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-11` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: Mid`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Customer notes & attachments
```

---

## Description

```
As an agent, I want to add notes and attach files to a customer, so that important information is kept in one place.
```

---

## Acceptance criteria

```
1. Adding a note shows it with author and time.
2. Uploading an allowed file type up to 10 MB is saved and downloadable.
3. A file over 10 MB returns 400.
4. A disallowed file type (e.g. .exe) returns 400.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1..CRM-10 (CRM-10 on branch `feature/group-a-customers`, same group).
- **Depends on code areas or other stories:** CRM-10's `IInteractionRecorder` / `InteractionType.Note` / `InteractionType.Attachment` / `InteractionEvents` and the customer details page (`CustomerDetailsPage`, `CustomerTimeline`); CRM-8's customer child-entity rules (FK `CustomerId` `Restrict`, routes under `/api/customers/{id:guid}/…`, deleted customer → 404); permissions `customers.view` (read, download) / `customers.manage` (add note, upload) — CRM-7 catalogue line 47 lists "CRM-11 notes/files".

## Extra notes (optional)

- Notes and attachments appear in the CRM-10 timeline (types `note`, `attachment`).
- Tests first (TDD); file rules (size, allowed types) live in Application and are unit-tested without a database or disk.

## Technical hints (optional)

- Local file storage behind an `IFileStorage` interface (root path from configuration, outside the repository by default, e.g. under the user's local app data; tests use a temp directory or an in-memory fake).
- Size limit 10 MB; allow-list of file types (by extension); downloads only through an authorized endpoint (no public static files).

## Out of scope

- Editing or deleting notes and attachments; rich-text notes; virus scanning; cloud storage (Azure Blob / S3); image previews; attachments on tickets (later ticket stories reuse `IFileStorage`).
