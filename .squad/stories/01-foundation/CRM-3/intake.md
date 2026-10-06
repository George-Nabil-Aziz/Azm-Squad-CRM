# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/01-foundation/CRM-3/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Foundation
- **Feature slug (folder under `plans/`):** `01-foundation`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-3` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 1`, `Priority: High`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
App layout with shadcn/ui (sidebar, header, routing)
```

---

## Description

```
As a staff user, I want a consistent app shell (login page, sidebar, header) built with shadcn/ui, so that I can navigate the CRM easily.
```

---

## Acceptance criteria

```
1. Visiting / while logged out redirects to /login.
2. After a successful login, the user lands on the dashboard and the sidebar shows the navigation items.
3. Logout clears the token and returns to /login.
4. Theme colors are defined as CSS variables (ready for branding).
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-1 (project skeleton), CRM-5 (global error handling, sonner toaster) and CRM-2 (JWT auth, client `src/auth/` session) — all done and merged to `main`.
- **Depends on code areas or other stories:** `client/` only: `client/src/App.tsx` (inline auth switch), `client/src/features/auth/` (minimal `LoginForm` / `CurrentUserPanel` from CRM-2), `client/src/auth/` (`signIn`, `signOut`, `useIsAuthenticated`, session store), `client/src/api/` (typed client, `getCurrentUser`, `getHealth`), `client/src/components/ApiErrorToaster.tsx`. Runs **before** CRM-4 (ar/en + RTL), CRM-6 (user management), CRM-7 (roles & permissions) and every feature page.

## Extra notes (optional)

- Phase 1 execution order: CRM-1 ✅, CRM-5 ✅, CRM-2 ✅, **CRM-3**, CRM-4, CRM-6, CRM-7, then customers (CRM-8..11), tickets (CRM-12..18), SLA (CRM-19..22), email/WhatsApp (CRM-23..26).
- Tests first (TDD): each acceptance criterion gets a test written and seen failing before the code that makes it pass.
- CRM-5 plan: CRM-3 runs `shadcn add sonner` and swaps **one import** in `ApiErrorToaster.tsx`; it must not reinstall `sonner` or add another toast mechanism.
- CRM-2 plan: CRM-3 replaces `LoginForm` / `CurrentUserPanel` and the inline auth switch in `App.tsx` with the styled login page, layout and `react-router` routes; it reuses `signIn`, `signOut`, `useIsAuthenticated`, `getCurrentUser` and keeps "no toast on 401".
- Navigation items for future areas may be listed, but must not link to pages that do not exist (decide: hide them or route them to a "coming soon" placeholder).

## Technical hints (optional)

- `CLAUDE.md` "Architecture decisions" (frontend): routing `react-router`, server state `@tanstack/react-query`, forms `react-hook-form` + `zod`, toasts shadcn `sonner`; pages in `client/src/pages/<area>/`, feature components in `client/src/features/<feature>/`.
- `CLAUDE.md` frontend rules: shadcn/ui components in `client/src/components/ui` (no other UI library), colors only via theme CSS variables (no hex in components), logical Tailwind classes (`ms-`/`me-`/`ps-`/`pe-`/`start-`/`end-`), API calls only through `client/src/api`, tests by role/label, follow `vercel-react-best-practices`.
- Tailwind CSS v4 + shadcn/ui CLI on Vite (non-interactive commands). The shadcn MCP server is configured in `.mcp.json`.

## Out of scope

- Arabic / RTL switching and translation files (CRM-4) — strings stay in temporary English message modules that CRM-4 moves.
- Real pages for tickets, customers, knowledge base, reports, users (their own stories).
- Hiding menu items by permission (CRM-7); user management (CRM-6).
- Dark-mode toggle, custom branding UI, refresh tokens, "remember me".
- Backend changes.
