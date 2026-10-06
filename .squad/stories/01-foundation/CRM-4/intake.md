# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/01-foundation/CRM-4/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Foundation
- **Feature slug (folder under `plans/`):** `01-foundation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-4` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Arabic / English with RTL support
```

---

## Description

```
As a user, I want to switch between Arabic and English, so that I can use the CRM in my language with correct layout direction.
```

---

## Acceptance criteria

```
1. Switching to Arabic sets <html dir="rtl" lang="ar">; switching to English sets dir="ltr" lang="en".
2. All UI strings come from translation files (no hard-coded text in components).
3. The selected language persists after page reload.
4. API validation messages follow the Accept-Language header (ar / en).
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1 (project skeleton), CRM-5 (global error handling: ProblemDetails, FluentValidation, `error-messages.ts`), CRM-2 (JWT auth, `auth-messages.ts`) and CRM-3 (shadcn/ui layout, routing, `app/messages.ts`, login schema) — all done and merged to `main`.
- **Depends on code areas or other stories:** `client/` (all message modules, layout, login form, API client, `index.html`, `index.css` font) and `server/` (`Program.cs` middleware order, `GlobalExceptionHandler`, `LoginRequestValidator`, `AuthService` messages). Runs **before** CRM-6 (user management), CRM-7 (roles & permissions) and every feature page, which all add their strings in both languages.

## Extra notes (optional)

- Phase 1 execution order: CRM-1 ✅, CRM-5 ✅, CRM-2 ✅, CRM-3 ✅, **CRM-4**, CRM-6, CRM-7, then customers (CRM-8..11), tickets (CRM-12..18), SLA (CRM-19..22), email/WhatsApp (CRM-23..26).
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass.
- CRM-5 plan: CRM-4 replaces `client/src/api/error-messages.ts` strings with i18n keys and passes `dir` from the current language to the `Toaster`.
- CRM-2 plan: CRM-4 moves `client/src/features/auth/auth-messages.ts` into `client/src/i18n/{en,ar}.json`.
- CRM-3 plan: CRM-4 moves `client/src/app/messages.ts` + `auth-messages.ts` + `error-messages.ts` to i18n; `NavigationItem.id` becomes the translation key; `loginSchema` (built at module load with English messages) must become translatable; set `<html lang dir>`, pass `dir` and `side` to `<Sidebar>`; pick a font with Arabic glyphs (Geist has none); decide how to translate shadcn's hard-coded sr-only texts in `components/ui/sidebar.tsx`.
- Add a guard test that fails when a component contains hard-coded user-facing text (where feasible), or document the convention clearly.
- The client must send `Accept-Language` with every API call.

## Technical hints (optional)

- `CLAUDE.md` "Architecture decisions" (frontend): i18n with `react-i18next`, translation files `client/src/i18n/{ar,en}.json`. Frontend rules: no hard-coded user-facing text (all strings in `ar` and `en`), logical Tailwind classes, colors only through theme variables, API calls only through `client/src/api`, tests by role/label.
- Backend: request localization (`ar`, `en`; default `en`) from `Accept-Language`; FluentValidation validation messages and ProblemDetails titles/details in the request language. Application layer must not reference ASP.NET Core.
- shadcn/ui is initialised with `rtl: true` (logical classes in generated components).

## Out of scope

- Languages other than Arabic and English; per-user language stored on the server (user profile, CRM-6 or later).
- Translating data entered by users (customer names, ticket text) and email/WhatsApp templates (their own stories).
- Localized number/date formatting beyond what the existing screens show (no dates are shown yet).
- Dark-mode toggle, branding UI.
