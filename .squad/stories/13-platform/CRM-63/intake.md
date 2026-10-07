# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/13-platform/CRM-63/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Platform
- **Feature slug (folder under `plans/`):** `13-platform`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-63` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** ``
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Custom branding (colors & logo)
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a SuperAdmin, I want to set the logo and brand colors from the UI, so that the CRM matches our identity without code changes.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
1. A SuperAdmin uploads a logo and sets primary and secondary colors.
2. Colors are applied through CSS variables (shadcn/ui theme) without rebuilding the app.
3. An invalid color returns 400; a logo over 2 MB returns 400.
4. Branding applies to the staff app, the customer portal, and emails.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| *(e.g. `attachments/flow.png`)* | *(e.g. UX flow)* |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** (tracker ids only; optional short note)
- **Depends on code areas or other stories:**

## Extra notes (optional)

- Anything not captured above (e.g. chat context) — keep short.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.

## Out of scope

- What this story explicitly does **not** cover:
