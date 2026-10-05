# Story 04 — App layout with shadcn/ui (sidebar, header, routing) (Story: CRM-3)

## Prerequisites

- Story 01 completed: [01-story-project-skeleton.md](01-story-project-skeleton.md) (CRM-1) — `client/` Vite + React + TypeScript app, Vitest + RTL, oxlint, Vite proxy `/api` → `http://localhost:5080`. Merged to `main`.
- Story 02 completed: [02-story-global-error-handling.md](02-story-global-error-handling.md) (CRM-5) — client `ApiError` / `onApiError` / `ApiErrorToaster` (sonner). Its section "7 — How CRM-3 builds on this" is **binding**: run `shadcn add sonner`, change **one import** in `ApiErrorToaster.tsx`, keep `toast` from `sonner`, do **not** reinstall `sonner` or add another toast mechanism. Merged to `main`.
- Story 03 completed: [03-story-authentication.md](03-story-authentication.md) (CRM-2) — `client/src/auth/` (`session.ts`, `sign-in.ts`, `useIsAuthenticated.ts`), `client/src/api/auth.ts` (`login`, `getCurrentUser`), 401 never toasts, 401 on a signed-in call clears the session. Its section "6 — How later stories build on this" is **binding**: CRM-3 replaces `LoginForm` / `CurrentUserPanel` and the inline auth switch in `App.tsx` with the styled login page, layout and `react-router` routes, reuses `signIn`, `signOut`, `useIsAuthenticated`, `getCurrentUser` unchanged, and keeps "no toast on 401". Merged to `main`.
- Work on branch **`feature/crm-3-app-layout`** (already created from `main`).
- Phase 1 order: CRM-1 ✅ → CRM-5 ✅ → CRM-2 ✅ → **CRM-3 (this)** → CRM-4 (ar/en + RTL) → CRM-6 (users) → CRM-7 (roles & permissions) → customers → tickets → SLA → email/WhatsApp.
- **Network access** is required once: `npx shadcn@4.21.2 …` downloads the CLI and reads the shadcn registry (ui.shadcn.com).
- **Shared contract created here** (every later frontend story depends on it): Tailwind v4 + shadcn/ui setup (`components.json`, `@/` path alias, theme CSS variables in `client/src/index.css`), the route table `client/src/app/AppRoutes.tsx`, the sidebar item list `client/src/app/navigation.ts`, the layout `client/src/components/layout/`, the QueryClient factory `client/src/app/query-client.ts`, the shared test helper `client/src/test/fake-api.ts`, and the guard tests in `client/src/theme.test.ts` (no hex colors, logical direction classes).

---

## Story Goal

Staff users get one consistent app shell built with shadcn/ui: a styled login page, a sidebar with the CRM areas, a header with the signed-in user and "Sign out", and URL routing.

1. Visiting `/` (or any protected path) while signed out redirects to **`/login`** (AC 1). The requested path is remembered and opened after login.
2. After a successful login the user lands on the **dashboard** (`/`, heading "Dashboard") and the sidebar shows the navigation items "Dashboard", "Tickets", "Customers", "Knowledge base", "Reports", "Users" (AC 2).
3. "Sign out" in the header clears the token and returns to **`/login`** (AC 3). The React Query cache is cleared on every sign-out (button, expired or rejected token).
4. Theme colors are **CSS variables** (`:root` + `.dark` in `client/src/index.css`, mapped to Tailwind colors in `@theme inline`), so branding later only overrides variables (AC 4). Guard tests forbid hex colors in source files and physical direction classes (`ml-`, `mr-`, `left-`, …) in app components.
5. Login form: shadcn `Card` + `Field` + `Input` + `Button`, **react-hook-form + zod** (inline "Enter your email." / "Enter a valid email address." / "Enter your password." without calling the API), wrong credentials inline "Invalid email or password." with **no toast** (CRM-2 rule kept).
6. **Navigation decision:** every CRM area is listed in the sidebar now, and every item links to a route that exists. Areas whose story is not built yet (`/tickets`, `/customers`, `/knowledge-base`, `/reports`, `/users`) render one shared **`ComingSoonPage`** ("This area is coming soon."). Each later story replaces its route line in `AppRoutes.tsx` with the real page. A test clicks every sidebar link and asserts a page with that heading opens, so a dead link fails the build.
7. The CRM-1 "API status" moves to the dashboard placeholder (card "API status": "ok" / "unavailable"), loaded with React Query.

**Not in scope:** translations, `dir="rtl"`, sidebar side switching and the Arabic font (CRM-4 — strings stay in temporary English message modules); real area pages (their stories); hiding menu items by permission (CRM-7); dark-mode toggle (`.dark` variables exist, no switcher); branding UI; route-level code splitting; backend changes.

---

## Context — Read These Files First

1. `CLAUDE.md` — **Frontend rules** lines 50–58 (shadcn/ui only in `client/src/components/ui`, colors only via theme CSS variables, logical Tailwind classes, API calls only through `client/src/api`, tests by role/label, `vercel-react-best-practices`). **Architecture decisions → Frontend** lines 74–77 (binding): `react-router`, `@tanstack/react-query`, `react-hook-form` + `zod`, shadcn `sonner`; token only in `client/src/auth/`; pages in `client/src/pages/<area>/`, feature components in `client/src/features/<feature>/`.
2. `.squad/stories/foundation/CRM-3/intake.md` — acceptance criteria 1–4 and **Out of scope**.
3. [02-story-global-error-handling.md](02-story-global-error-handling.md) lines 1143–1149 — "How CRM-3 builds on this" (sonner wrapper, one import).
4. [03-story-authentication.md](03-story-authentication.md) lines 1833–1843 — "How later stories build on this" (CRM-3 bullet).
5. `client/src/App.tsx` — whole file (39 lines). Lines 14–22 health `useEffect` (moves to `DashboardPage` as a React Query query), line 32 inline switch `isAuthenticated ? <CurrentUserPanel /> : <LoginForm />` (replaced by routes), line 34 `<ApiErrorToaster />` (stays, mounted once at the root). File is **replaced**.
6. `client/src/components/ApiErrorToaster.tsx` — line 2 `import { Toaster, toast } from 'sonner'` (the one import to change), lines 15–17 the 401 early return (**keep**), line 23 `<Toaster position="top-center" closeButton />` (unchanged).
7. `client/src/auth/session.ts` — lines 35–41 `getAccessToken()`, lines 61–72 `subscribeToSession(listener)` (returns an unsubscribe function; used by `App` to clear the query cache). **No change.**
8. `client/src/auth/sign-in.ts` — lines 5–12 `signIn(email, password)` / `signOut()`. **No change.** `client/src/auth/useIsAuthenticated.ts` lines 9–11. **No change.**
9. `client/src/api/auth.ts` — lines 26–28 `getCurrentUser(signal?)`; `client/src/api/health.ts` lines 7–9 `getHealth(signal?)`; `client/src/api/errors.ts` lines 29–31 `isApiError`. **No change.**
10. `client/src/api/client.ts` — lines 59–61: a 401 on a signed-in call calls `clearSession()`; this is what makes `RequireAuth` redirect to `/login` when the stored token is rejected. **No change** (still the only `fetch` caller).
11. `client/src/features/auth/LoginForm.tsx` (43 lines, **replaced**), `client/src/features/auth/CurrentUserPanel.tsx` (27 lines, **deleted** — its user name + sign-out move to `AppHeader`), `client/src/features/auth/auth-messages.ts` (12 lines, **replaced**; `signedInAs` and `account` are no longer used and are removed).
12. `client/src/App.test.tsx` (24 lines, **deleted** — its two health tests move to `DashboardPage.test.tsx`), `client/src/App.auth.test.tsx` (88 lines; lines 15–30 `fakeApi()` moves to `client/src/test/fake-api.ts`; file **replaced**), `client/src/App.toast.test.tsx` (29 lines, **replaced**: signed in, health fails on the dashboard).
13. `client/src/test/setup.ts` — lines 5–8 `afterEach(cleanup + localStorage.clear())`; you add a `matchMedia` stub and a URL reset.
14. `client/src/index.css` — 111 lines of Vite boilerplate (`--text`, `--accent`, hex colors, `#root { width: 1126px … }`). **Replaced entirely** (first by one line, then by `shadcn init`).
15. `client/vite.config.ts` (17 lines) — line 6 `plugins: [react()]`; lines 7–12 proxy (keep the comment and target); lines 13–16 `test` block (keep).
16. `client/tsconfig.json` (7 lines) and `client/tsconfig.app.json` — line 23 `"noFallthroughCasesInSwitch": true` is the last compiler option; the `paths` alias goes after it. Line 22 `"erasableSyntaxOnly": true` (no enums / parameter properties). TypeScript is **~6.0**: `paths` works **without** `baseUrl` (do not add `baseUrl`, it is deprecated in TS 6).
17. `client/.oxlintrc.json` (8 lines) — line 6 `react/only-export-components` (warn): `.tsx` files export only components; hooks/helpers live in `.ts` files.
18. `client/index.html` — line 2 `<html lang="en">` (CRM-4 sets `lang`/`dir`), line 7 `<title>client</title>`.
19. `.mcp.json` — shadcn MCP server (`npx shadcn@latest mcp`); optional for looking up components. The plan pins the CLI to **4.21.2** instead of `@latest`.
20. `.claude/skills/vercel-react-best-practices/SKILL.md` — follow it (direct imports, no barrel `index.ts`, lazy state init `useState(createQueryClient)`, no inline component definitions).

Verified while planning (fresh scratch clone of `main` in the session scratchpad, the exact commands and files below were replayed from scratch; **`npm test` 84 passed in 11 files, `npm run build` and `npm run lint` clean (exit 0), `dotnet test` 40 passed**):

- npm (latest on 2026-10-06): **shadcn 4.21.2** (CLI), **tailwindcss 4.3.3**, **@tailwindcss/vite 4.3.3** (peer `vite ^5.2 || ^6 || ^7 || ^8` — fine with Vite 8), **react-router 8.4.0** (peer `react >=19.2.7`, engines `node >=22.22` — Node 24.15 installed), **@tanstack/react-query 5.104.1**, **react-hook-form 7.89.0**, **zod 4.6.5**, **@hookform/resolvers 5.9.1** (has Zod 4 overloads).
- `shadcn init` refuses to run until Tailwind is installed **and** `@/*` is in `tsconfig.json` (`✖ Validating Tailwind CSS` / `✖ Validating import alias`), so those come first.
- `npx shadcn@4.21.2 init -t vite -b radix -p nova --rtl -y` runs **without prompts**. It writes `components.json` (`"style": "radix-nova"`, `"rtl": true`, `"iconLibrary": "lucide"`, aliases `@/components`, `@/lib/utils`, `@/components/ui`, `@/lib`, `@/hooks`), `src/components/ui/button.tsx`, `src/lib/utils.ts` (`export { cn } from "cn"` — `cn` is shadcn's clsx + tailwind-merge replacement), rewrites `src/index.css` (imports `tailwindcss`, `tw-animate-css`, `shadcn/tailwind.css`, `@fontsource-variable/geist`; `@theme inline` color mapping; `:root` and `.dark` color variables in `oklch(...)`, no hex), and adds dependencies `@fontsource-variable/geist`, `class-variance-authority`, `cn`, `lucide-react`, `radix-ui`, `shadcn`, `tw-animate-css`. `--rtl` makes the generated components use logical classes (`border-e`, `ms-4`, `rtl:rotate-180`, …).
- `npx shadcn@4.21.2 add sonner sidebar card input label field -y` creates `sonner.tsx`, `card.tsx`, `input.tsx`, `label.tsx`, `separator.tsx`, `tooltip.tsx`, `skeleton.tsx`, `sheet.tsx`, `field.tsx`, `sidebar.tsx` in `src/components/ui/` and `src/hooks/use-mobile.ts`, skips the identical `button.tsx`, and adds `next-themes`. It prints a reminder to wrap the app in `TooltipProvider` — **not needed** here: no `tooltip` prop is passed to `SidebarMenuButton`, so no `Tooltip` is rendered.
- The shadcn `Toaster` (`sonner.tsx`) reads `useTheme()` from `next-themes`; without a `ThemeProvider` the theme is `"system"`, and sonner then calls `window.matchMedia`. `useIsMobile()` (used by `SidebarProvider`) calls it too. **jsdom has no `matchMedia`**: without the stub in `setup.ts`, 19 tests fail with `TypeError: window.matchMedia is not a function`.
- **Vitest does not process CSS**: `import css from './index.css?raw'` yields `""` in tests; `new URL('./index.css', import.meta.url)` is not a `file:` URL under jsdom. The theme test therefore reads the file with `readFileSync(join(import.meta.dirname, 'index.css'))` (`/// <reference types="node" />`; `@types/node` is already a devDependency). `import.meta.glob(..., { query: '?raw', import: 'default', eager: true })` **does** work for `.ts`/`.tsx` files.
- oxlint reports 3 warnings in generated files (`button.tsx` exports `buttonVariants`, `sidebar.tsx` exports `useSidebar` → `only-export-components`; `use-mobile.ts` → `react(set-state-in-effect)`). An `overrides` entry for `src/components/ui/**` and `src/hooks/use-mobile.ts` turns those two rules off there; result: no output, exit 0.
- `npm run build` succeeds; Vite prints the **warning** "Some chunks are larger than 500 kB after minification" (577 kB, 183 kB gzip). It is a warning, not an error; route-level code splitting is out of scope (see "How later stories build on this").
- `npm audit` lists 7 high-severity advisories, all inside the `shadcn` CLI's own dependency tree (`ts-morph` → `fast-glob`). The app only imports `shadcn/tailwind.css` at build time, so the plan moves `shadcn` to **devDependencies**; nothing from it ships to the browser. Do **not** run `npm audit fix --force`.
- react-router 8.4.0 exports `BrowserRouter`, `Routes`, `Route`, `Navigate`, `Outlet`, `NavLink`, `useLocation`, `useMatch` from `'react-router'` (no `react-router-dom`). `NavLink` sets `aria-current="page"` on the active link. No warnings were printed during the test run.

---

## Frontend Tasks

All commands run from `client/`. Follow `vercel-react-best-practices` (direct imports, no barrel `index.ts`). New app files import with the `@/` alias; existing files keep their relative imports (only `ApiErrorToaster.tsx` gets one new `@/` import).

### 1 — Tests first (Red)

**File: `client/src/test/setup.ts`** — final content:

```ts
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// jsdom has no matchMedia. shadcn's sidebar (useIsMobile) and sonner's "system" theme call it.
// matches: false → desktop layout, light theme.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  configurable: true,
  value: (query: string): MediaQueryList => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }),
})

afterEach(() => {
  cleanup()
  localStorage.clear()
  // BrowserRouter reads the real URL: start every test at "/".
  window.history.replaceState(null, '', '/')
})
```

**Create file: `client/src/test/fake-api.ts`** (shared by the App-level tests; `.ts`, not `.tsx`)

```ts
import { fireEvent, screen } from '@testing-library/react'
import { vi } from 'vitest'

export const ADMIN_PASSWORD = 'Admin#12345'

export const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

function json(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

const me = { id: '1', email: 'admin@crm.local', fullName: 'System Administrator', roles: ['SuperAdmin'] }

interface FakeApiOptions {
  /** Status returned by GET /api/health (default 200). */
  healthStatus?: number
}

/**
 * Fake API behind a stubbed fetch: login accepts ADMIN_PASSWORD and returns "good-token";
 * /api/auth/me needs that token; /api/health is ok unless healthStatus says otherwise.
 */
export function fakeApi({ healthStatus = 200 }: FakeApiOptions = {}) {
  return vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/health') {
      return healthStatus === 200
        ? json(200, { status: 'ok' })
        : json(healthStatus, { status: healthStatus, correlationId: 'health-1' }, 'application/problem+json')
    }
    if (path === '/api/auth/login') {
      const { password } = JSON.parse(String(init?.body)) as { password: string }
      return password === ADMIN_PASSWORD
        ? json(200, { accessToken: 'good-token', tokenType: 'Bearer', expiresAt: inOneHour() })
        : json(401, { status: 401, title: 'Authentication failed.', correlationId: 'c-401' }, 'application/problem+json')
    }
    if (path === '/api/auth/me') {
      const auth = ((init?.headers ?? {}) as Record<string, string>).Authorization
      return auth === 'Bearer good-token' ? json(200, me) : json(401, { status: 401 }, 'application/problem+json')
    }
    return json(404, { status: 404 }, 'application/problem+json')
  })
}

/** Number of fetch calls made to `path`. */
export function callsTo(fetchMock: ReturnType<typeof fakeApi>, path: string): number {
  return fetchMock.mock.calls.filter(([calledPath]) => calledPath === path).length
}

export function submitSignIn(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}
```

**Create file: `client/src/App.layout.test.tsx`** — AC 1, 2, 3 through the real `App`, the real API client and a fake `fetch`.

```tsx
import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { getAccessToken, saveSession } from './auth/session'
import { ADMIN_PASSWORD, callsTo, fakeApi, inOneHour, submitSignIn } from './test/fake-api'

const NAVIGATION_LABELS = ['Dashboard', 'Tickets', 'Customers', 'Knowledge base', 'Reports', 'Users']

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

function signedIn() {
  saveSession('good-token', inOneHour())
}

describe('App layout and routing', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('redirects / to /login when signed out', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
  })

  it('lands on the dashboard with the sidebar navigation after a successful login', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
    const navigation = screen.getByRole('navigation', { name: 'Main navigation' })
    const links = within(navigation).getAllByRole('link')
    expect(links.map((link) => link.textContent)).toEqual(NAVIGATION_LABELS)
    expect(within(navigation).getByRole('link', { name: 'Dashboard' })).toHaveAttribute('aria-current', 'page')
  })

  it('shows the signed-in user in the header', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    expect(await screen.findByText('System Administrator')).toBeInTheDocument()
  })

  it('signs out: clears the token and returns to /login', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
    expect(getAccessToken()).toBeNull()
  })

  it('loads the current user again after signing out and back in', async () => {
    signedIn()
    const fetchMock = fakeApi()
    vi.stubGlobal('fetch', fetchMock)
    renderAt('/')
    await screen.findByText('System Administrator')
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)

    expect(await screen.findByText('System Administrator')).toBeInTheDocument()
    expect(callsTo(fetchMock, '/api/auth/me')).toBe(2)
  })

  it('returns to /login when the stored token is rejected', async () => {
    saveSession('expired-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
  })

  it('opens the page the user asked for after login', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/customers')
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)

    expect(await screen.findByRole('heading', { level: 1, name: 'Customers' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/customers')
  })

  it('redirects /login to the dashboard when already signed in', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/login')

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
  })

  it('redirects an unknown path to the dashboard', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/no-such-page')

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
  })

  it('opens a page for every navigation item (areas not built yet say "coming soon")', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })

    for (const label of NAVIGATION_LABELS.slice(1)) {
      fireEvent.click(within(navigation).getByRole('link', { name: label }))

      expect(await screen.findByRole('heading', { level: 1, name: label })).toBeInTheDocument()
      expect(screen.getByText('This area is coming soon.')).toBeInTheDocument()
      expect(within(navigation).getByRole('link', { name: label })).toHaveAttribute('aria-current', 'page')
    }
  })
})
```

**File: `client/src/App.auth.test.tsx`** — replace the whole file (the sign-in / sign-out flow tests moved to `App.layout.test.tsx`; this file now covers the login form):

```tsx
import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { callsTo, fakeApi, submitSignIn } from './test/fake-api'

describe('Login page', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the sign-in form with the app name when signed out', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Customer Support CRM' })).toBeInTheDocument()
  })

  it('shows an inline error and no toast for a wrong password', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', 'wrong')

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(screen.queryByText('Something went wrong. Please try again.')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('admin@crm.local')
  })

  it('shows field errors and does not call the API when the fields are empty', async () => {
    const fetchMock = fakeApi()
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)
    await screen.findByRole('form', { name: 'Sign in' })

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Enter your email.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
    expect(callsTo(fetchMock, '/api/auth/login')).toBe(0)
  })

  it('shows a field error for an invalid email address', async () => {
    const fetchMock = fakeApi()
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('not-an-email', 'whatever')

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(callsTo(fetchMock, '/api/auth/login')).toBe(0)
  })
})
```

**File: `client/src/App.toast.test.tsx`** — replace the whole file:

```tsx
import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'
import { fakeApi, inOneHour } from './test/fake-api'

// No module mocks here: the real API client runs against a stubbed fetch,
// so this proves the whole path API failure → toast on the dashboard.
describe('App error toast', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows an error toast when the health call fails', async () => {
    saveSession('good-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi({ healthStatus: 500 }))

    render(<App />)

    expect(await screen.findByText('unavailable')).toBeInTheDocument()
    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
    expect(screen.getByText('Reference: health-1')).toBeInTheDocument()
  })
})
```

**Delete file: `client/src/App.test.tsx`** (its two health tests move here ↓).

**Create file: `client/src/pages/dashboard/DashboardPage.test.tsx`**

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser } from '@/api/auth'
import { getHealth } from '@/api/health'
import { createQueryClient } from '@/app/query-client'
import { DashboardPage } from './DashboardPage'

vi.mock('@/api/health', () => ({ getHealth: vi.fn() }))
vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

function renderDashboard() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <DashboardPage />
    </QueryClientProvider>,
  )
}

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.mocked(getHealth).mockReset()
    vi.mocked(getCurrentUser).mockResolvedValue({
      id: '1',
      email: 'admin@crm.local',
      fullName: 'System Administrator',
      roles: ['SuperAdmin'],
    })
  })

  it('welcomes the signed-in user', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    renderDashboard()

    expect(screen.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(await screen.findByText('Welcome, System Administrator')).toBeInTheDocument()
  })

  it('shows "ok" when the API is healthy', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    renderDashboard()

    expect(await screen.findByText('ok')).toBeInTheDocument()
  })

  it('shows "unavailable" when the API call fails', async () => {
    vi.mocked(getHealth).mockRejectedValue(new Error('503'))
    renderDashboard()

    expect(await screen.findByText('unavailable')).toBeInTheDocument()
  })
})
```

**Create file: `client/src/app/return-path.test.ts`**

```ts
import { describe, expect, it } from 'vitest'
import { getReturnPath } from './return-path'

describe('getReturnPath', () => {
  it.each([
    [undefined, '/'],
    [null, '/'],
    [{}, '/'],
    [{ from: 42 }, '/'],
    [{ from: '/customers' }, '/customers'],
    [{ from: '/tickets?status=open' }, '/tickets?status=open'],
    [{ from: '//evil.example' }, '/'],
    [{ from: '/\\evil.example' }, '/'],
    [{ from: 'https://evil.example' }, '/'],
    [{ from: '/login' }, '/'],
  ])('state %j → %s', (state, expected) => {
    expect(getReturnPath(state)).toBe(expected)
  })
})
```

**Create file: `client/src/theme.test.ts`** — AC 4 + the CLAUDE.md color / RTL rules as guard tests.

```ts
/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

// Read from disk: Vitest does not process CSS, so `import css from './index.css?raw'` yields an empty string.
const css = readFileSync(join(import.meta.dirname, 'index.css'), 'utf8')

// Every app source file (tests and test helpers excluded) as text, keyed by path relative to src/.
const sources = import.meta.glob<string>(['./**/*.{ts,tsx}', '!./**/*.test.{ts,tsx}', '!./test/**'], {
  query: '?raw',
  import: 'default',
  eager: true,
})

/** Text of the first `<selector> { … }` block in index.css (up to its first closing brace). */
function block(selector: string): string {
  const start = css.indexOf(`${selector} {`)
  expect(start, `"${selector} {" in index.css`).toBeGreaterThanOrEqual(0)
  return css.slice(start, css.indexOf('}', start))
}

const THEME_COLORS = [
  'background',
  'foreground',
  'card',
  'popover',
  'primary',
  'primary-foreground',
  'secondary',
  'muted',
  'muted-foreground',
  'accent',
  'destructive',
  'border',
  'input',
  'ring',
  'sidebar',
  'sidebar-foreground',
  'sidebar-primary',
  'sidebar-accent',
]

describe('theme', () => {
  it.each(THEME_COLORS)('defines --%s as a CSS variable for light and dark mode', (name) => {
    expect(block(':root')).toContain(`--${name}:`)
    expect(block('.dark')).toContain(`--${name}:`)
  })

  it.each(THEME_COLORS)('maps the Tailwind color "%s" to its CSS variable', (name) => {
    expect(block('@theme inline')).toContain(`--color-${name}: var(--${name});`)
  })

  it('scans the app sources', () => {
    expect(Object.keys(sources)).toContain('./App.tsx')
    expect(Object.keys(sources)).toContain('./components/ui/button.tsx')
  })

  it('uses no hard-coded hex colors in source files', () => {
    const offenders = Object.entries(sources)
      .filter(([, text]) => /#[0-9a-fA-F]{3,8}\b/.test(text))
      .map(([path]) => path)
    expect(offenders).toEqual([])
  })

  it('uses logical (RTL-safe) direction classes in app components', () => {
    const physical =
      /(?<![\w-])-?(?:ml|mr|pl|pr|left|right)-[\w[]|(?<![\w-])(?:text|float)-(?:left|right)\b|(?<![\w-])(?:border|rounded)-[lr](?:-|\b)/
    const offenders = Object.entries(sources)
      // components/ui is generated by the shadcn CLI (initialised with --rtl) and is not edited by hand.
      .filter(([path]) => !path.startsWith('./components/ui/'))
      .filter(([, text]) => physical.test(text))
      .map(([path]) => path)
    expect(offenders).toEqual([])
  })
})
```

`fireEvent` comes with `@testing-library/react`; **do not** add `@testing-library/user-event`.

Run `npm test` → **Red**: 49 failed, 22 passed (71) — `App.layout.test.tsx` (all 10: no routing), `App.auth.test.tsx` (2 field-error tests: no zod), `theme.test.ts` (36 variable tests + "scans the app sources": no shadcn yet), `return-path.test.ts` and `DashboardPage.test.tsx` (modules do not exist). `App.toast.test.tsx`, the two first `App.auth` tests and all CRM-1/2/5 unit tests still pass.

### 2 — Tailwind CSS v4 + path alias (tooling, no app code)

```bash
npm install -D tailwindcss@4.3.3 @tailwindcss/vite@4.3.3
```

**File: `client/src/index.css`** — replace the whole file with exactly one line (`shadcn init` fills the rest in step 3):

```css
@import "tailwindcss";
```

**File: `client/vite.config.ts`** — final content:

```ts
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      // Must match applicationUrl of the "http" profile in server/src/Crm.Api/Properties/launchSettings.json
      '/api': 'http://localhost:5080',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
```

**File: `client/tsconfig.json`** — final content (the shadcn CLI reads the alias from this file):

```json
{
  "files": [],
  "compilerOptions": {
    "paths": {
      "@/*": ["./src/*"]
    }
  },
  "references": [
    { "path": "./tsconfig.app.json" },
    { "path": "./tsconfig.node.json" }
  ]
}
```

**File: `client/tsconfig.app.json`** — line 23: add a comma after `"noFallthroughCasesInSwitch": true` and add the alias (used by `tsc -b`). Lines 19–30 after the change:

```jsonc
    /* Linting */
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "erasableSyntaxOnly": true,
    "noFallthroughCasesInSwitch": true,

    /* Path alias (shadcn/ui) */
    "paths": {
      "@/*": ["./src/*"]
    }
  },
  "include": ["src"]
}
```

**No `baseUrl`** (deprecated in TypeScript 6; `paths` resolve relative to the tsconfig file).

### 3 — shadcn/ui init + components (tooling, generated files)

Run exactly (non-interactive; pinned CLI version):

```bash
npx shadcn@4.21.2 init -t vite -b radix -p nova --rtl -y
npx shadcn@4.21.2 add sonner sidebar card input label field -y
npm install -D shadcn@4.21.2
```

- `init` → `components.json`, `src/lib/utils.ts`, `src/components/ui/button.tsx`, new `src/index.css` (theme variables), dependencies (see "Verified while planning"). It ends with "Project initialization completed." and a link to the RTL docs (that is CRM-4).
- `add` → `src/components/ui/{sonner,card,input,label,separator,tooltip,skeleton,sheet,field,sidebar}.tsx`, `src/hooks/use-mobile.ts`, dependency `next-themes`. Ignore its `TooltipProvider` reminder (no tooltips are used).
- `npm install -D shadcn@4.21.2` moves the `shadcn` package from `dependencies` to `devDependencies` (only `shadcn/tailwind.css` is imported, at build time).
- **Do not edit** files in `src/components/ui/`, `src/lib/utils.ts`, `src/hooks/use-mobile.ts` or the generated `src/index.css` by hand. **Do not** run `npx shadcn add sonner` with `--overwrite` later.
- `sonner` stays at `^2.0.8` (already installed by CRM-5); the CLI does not reinstall it.

Then the runtime libraries from CLAUDE.md "Architecture decisions":

```bash
npm install react-router@8.4.0 @tanstack/react-query@5.104.1 react-hook-form@7.89.0 zod@4.6.5 @hookform/resolvers@5.9.1
```

**File: `client/.oxlintrc.json`** — final content (generated shadcn files are exempt from two React rules):

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  },
  "overrides": [
    {
      "files": ["src/components/ui/**", "src/hooks/use-mobile.ts"],
      "rules": {
        "react/only-export-components": "off",
        "react/set-state-in-effect": "off"
      }
    }
  ]
}
```

**File: `client/index.html`** — line 7: `<title>Customer Support CRM</title>`. Leave `lang="en"` (CRM-4).

Expected `client/package.json` dependency blocks after steps 2–3 (versions as installed; `npm` writes `^` ranges):

```json
  "dependencies": {
    "@fontsource-variable/geist": "^5.3.0",
    "@hookform/resolvers": "^5.9.1",
    "@tanstack/react-query": "^5.104.1",
    "class-variance-authority": "^0.7.1",
    "cn": "^0.4.0",
    "lucide-react": "^1.52.0",
    "next-themes": "^0.4.6",
    "radix-ui": "^1.6.7",
    "react": "^19.2.8",
    "react-dom": "^19.2.8",
    "react-hook-form": "^7.89.0",
    "react-router": "^8.4.0",
    "sonner": "^2.0.8",
    "tw-animate-css": "^1.4.0",
    "zod": "^4.6.5"
  },
  "devDependencies": {
    "@tailwindcss/vite": "^4.3.3",
    "@testing-library/jest-dom": "^7.0.1",
    "@testing-library/react": "^16.3.3",
    "@types/node": "^24.13.3",
    "@types/react": "^19.2.18",
    "@types/react-dom": "^19.2.7",
    "@vitejs/plugin-react": "^6.1.1",
    "jsdom": "^30.1.2",
    "oxlint": "^1.81.0",
    "shadcn": "^4.21.2",
    "tailwindcss": "^4.3.3",
    "typescript": "~6.0.2",
    "vite": "^8.3.0",
    "vitest": "^5.0.3"
  }
```

Run `npm test` → still **Red**, but `theme.test.ts` is now **green** (39 tests): 12 failed, 59 passed (71).

### 4 — Messages, navigation, QueryClient, return path (Green, part 1)

**Create file: `client/src/app/messages.ts`**

```ts
// Temporary English text for the app shell, navigation and dashboard.
// CRM-4 moves these strings to client/src/i18n/{en,ar}.json.
export const shellMessages = {
  appName: 'Customer Support CRM',
  mainNavigation: 'Main navigation',
  nav: {
    dashboard: 'Dashboard',
    tickets: 'Tickets',
    customers: 'Customers',
    knowledgeBase: 'Knowledge base',
    reports: 'Reports',
    users: 'Users',
  },
  comingSoon: 'This area is coming soon.',
  welcome: (name: string) => `Welcome, ${name}`,
  apiStatus: 'API status',
  apiLoading: 'loading',
  apiUnavailable: 'unavailable',
}

export type NavigationId = keyof typeof shellMessages.nav
```

**Create file: `client/src/app/navigation.ts`**

```ts
import {
  BookOpenIcon,
  ChartColumnIcon,
  LayoutDashboardIcon,
  TicketIcon,
  UserCogIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react'
import type { NavigationId } from './messages'

export interface NavigationItem {
  /** Key of the label in shellMessages.nav (CRM-4: translation key). */
  id: NavigationId
  /** Absolute route path. Every path here has a route in AppRoutes.tsx (real page or "coming soon"). */
  path: string
  icon: LucideIcon
}

/** Sidebar items, in display order. */
export const navigationItems: readonly NavigationItem[] = [
  { id: 'dashboard', path: '/', icon: LayoutDashboardIcon },
  { id: 'tickets', path: '/tickets', icon: TicketIcon },
  { id: 'customers', path: '/customers', icon: UsersIcon },
  { id: 'knowledgeBase', path: '/knowledge-base', icon: BookOpenIcon },
  { id: 'reports', path: '/reports', icon: ChartColumnIcon },
  { id: 'users', path: '/users', icon: UserCogIcon },
]
```

**Create file: `client/src/app/query-client.ts`**

```ts
import { QueryClient } from '@tanstack/react-query'

/** One QueryClient per App instance (tests get a fresh cache for every render). */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Every failed call already shows a toast (ApiErrorToaster); a retry would show it again.
        retry: false,
        staleTime: 30_000,
      },
    },
  })
}
```

**Create file: `client/src/app/return-path.ts`**

```ts
/** Router state that RequireAuth passes to /login: the page the user asked for. */
export interface LoginRedirectState {
  from: string
}

/**
 * Where to go after signing in: the stored in-app path, or "/" when it is missing or unsafe
 * (only same-origin paths starting with a single "/" are allowed; never /login itself).
 */
export function getReturnPath(state: unknown): string {
  const from = (state as Partial<LoginRedirectState> | null | undefined)?.from
  if (typeof from !== 'string') return '/'
  if (!from.startsWith('/') || from.startsWith('//') || from.startsWith('/\\')) return '/'
  if (from === '/login' || from.startsWith('/login?')) return '/'
  return from
}
```

### 5 — Auth feature: login form (react-hook-form + zod), current user (Green, part 2)

**File: `client/src/features/auth/auth-messages.ts`** — final content:

```ts
// Temporary English text. CRM-4 moves these strings to client/src/i18n/{en,ar}.json.
export const authMessages = {
  signInTitle: 'Sign in',
  signInDescription: 'Sign in with your work email and password.',
  email: 'Email',
  password: 'Password',
  signIn: 'Sign in',
  signingIn: 'Signing in…',
  emailRequired: 'Enter your email.',
  emailInvalid: 'Enter a valid email address.',
  passwordRequired: 'Enter your password.',
  invalidCredentials: 'Invalid email or password.',
  signOut: 'Sign out',
}
```

**Create file: `client/src/features/auth/login-schema.ts`**

```ts
import { z } from 'zod'
import { authMessages } from './auth-messages'

/** Client-side checks before calling POST /api/auth/login (the server validates again). */
export const loginSchema = z.object({
  email: z.string().trim().min(1, authMessages.emailRequired).pipe(z.email(authMessages.emailInvalid)),
  password: z.string().min(1, authMessages.passwordRequired),
})

export type LoginValues = z.infer<typeof loginSchema>
```

(`z.email(...)` is the Zod 4 top-level format; `.pipe` keeps "required" and "invalid" as two different messages.)

**Create file: `client/src/features/auth/useCurrentUser.ts`**

```ts
import { useQuery } from '@tanstack/react-query'
import { getCurrentUser } from '@/api/auth'

export const currentUserQueryKey = ['auth', 'me'] as const

/** The signed-in user (GET /api/auth/me). A 401 clears the session, so RequireAuth redirects to /login. */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserQueryKey,
    queryFn: ({ signal }) => getCurrentUser(signal),
  })
}
```

**File: `client/src/features/auth/LoginForm.tsx`** — replace the whole file:

```tsx
import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { isApiError } from '@/api/errors'
import { signIn } from '@/auth/sign-in'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { authMessages } from './auth-messages'
import { loginSchema, type LoginValues } from './login-schema'

/** Email + password form (react-hook-form + zod). On success the session changes and LoginPage redirects. */
export function LoginForm() {
  const [serverError, setServerError] = useState<string | null>(null)
  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  async function onSubmit(values: LoginValues) {
    setServerError(null)
    try {
      await signIn(values.email, values.password)
    } catch (caught) {
      // Wrong credentials are shown here; every other failure already raised a toast (ApiErrorToaster).
      if (isApiError(caught) && caught.status === 401) setServerError(authMessages.invalidCredentials)
    }
  }

  const isSubmitting = form.formState.isSubmitting

  return (
    <form aria-label={authMessages.signInTitle} noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <Controller
          name="email"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="login-email">{authMessages.email}</FieldLabel>
              <Input
                {...field}
                id="login-email"
                type="email"
                autoComplete="username"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
            </Field>
          )}
        />
        <Controller
          name="password"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="login-password">{authMessages.password}</FieldLabel>
              <Input
                {...field}
                id="login-password"
                type="password"
                autoComplete="current-password"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
            </Field>
          )}
        />
        {serverError ? <FieldError>{serverError}</FieldError> : null}
        <Button type="submit" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? authMessages.signingIn : authMessages.signIn}
        </Button>
      </FieldGroup>
    </form>
  )
}
```

- `noValidate` turns off browser validation so the zod messages show. `FieldError` renders `role="alert"` (that is what the wrong-password test finds).
- Controlled inputs (react-hook-form) keep the typed email after a wrong password (asserted by the test).

**Delete file: `client/src/features/auth/CurrentUserPanel.tsx`** (user name + sign-out move to `AppHeader`).

### 6 — Pages (Green, part 3)

**Create file: `client/src/pages/auth/LoginPage.tsx`**

```tsx
import { Navigate, useLocation } from 'react-router'
import { shellMessages } from '@/app/messages'
import { getReturnPath } from '@/app/return-path'
import { useIsAuthenticated } from '@/auth/useIsAuthenticated'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { authMessages } from '@/features/auth/auth-messages'
import { LoginForm } from '@/features/auth/LoginForm'

export function LoginPage() {
  const isAuthenticated = useIsAuthenticated()
  const location = useLocation()

  // Already signed in, or just signed in: go to the page the user asked for (default: dashboard).
  if (isAuthenticated) return <Navigate to={getReturnPath(location.state)} replace />

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl font-semibold">{shellMessages.appName}</h1>
          </CardTitle>
          <CardDescription>{authMessages.signInDescription}</CardDescription>
        </CardHeader>
        <CardContent>
          <LoginForm />
        </CardContent>
      </Card>
    </div>
  )
}
```

(`CardTitle` renders a `div`, so the real heading is the `h1` inside it.)

**Create file: `client/src/pages/dashboard/DashboardPage.tsx`**

```tsx
import { useQuery } from '@tanstack/react-query'
import { getHealth } from '@/api/health'
import { shellMessages } from '@/app/messages'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useCurrentUser } from '@/features/auth/useCurrentUser'

/** Placeholder dashboard: welcome line + API health. Real widgets come with the reports stories. */
export function DashboardPage() {
  const { data: user } = useCurrentUser()
  const health = useQuery({ queryKey: ['health'], queryFn: ({ signal }) => getHealth(signal) })

  const apiStatus = health.isError
    ? shellMessages.apiUnavailable
    : (health.data?.status ?? shellMessages.apiLoading)

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{shellMessages.nav.dashboard}</h1>
        {user ? <p className="text-muted-foreground">{shellMessages.welcome(user.fullName)}</p> : null}
      </div>
      <Card className="max-w-sm">
        <CardHeader>
          <CardTitle>{shellMessages.apiStatus}</CardTitle>
        </CardHeader>
        <CardContent>
          <p>{apiStatus}</p>
        </CardContent>
      </Card>
    </div>
  )
}
```

**Create file: `client/src/pages/coming-soon/ComingSoonPage.tsx`**

```tsx
import { shellMessages, type NavigationId } from '@/app/messages'

/** Placeholder for a sidebar area whose story is not built yet (keeps every navigation link working). */
export function ComingSoonPage({ area }: { area: NavigationId }) {
  return (
    <div className="flex flex-col gap-1">
      <h1 className="text-2xl font-semibold">{shellMessages.nav[area]}</h1>
      <p className="text-muted-foreground">{shellMessages.comingSoon}</p>
    </div>
  )
}
```

### 7 — Layout: sidebar + header (Green, part 4)

**Create file: `client/src/components/layout/AppSidebar.tsx`**

```tsx
import { NavLink, useMatch } from 'react-router'
import { shellMessages } from '@/app/messages'
import { navigationItems, type NavigationItem } from '@/app/navigation'
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'

function AppSidebarLink({ item }: { item: NavigationItem }) {
  const isRoot = item.path === '/'
  const isActive = useMatch({ path: item.path, end: isRoot }) !== null
  const { isMobile, setOpenMobile } = useSidebar()
  const Icon = item.icon

  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild isActive={isActive}>
        <NavLink to={item.path} end={isRoot} onClick={() => isMobile && setOpenMobile(false)}>
          <Icon aria-hidden="true" />
          <span>{shellMessages.nav[item.id]}</span>
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
  )
}

export function AppSidebar() {
  return (
    <Sidebar>
      <SidebarHeader>
        <span className="px-2 py-1 text-base font-semibold">{shellMessages.appName}</span>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <nav aria-label={shellMessages.mainNavigation}>
              <SidebarMenu>
                {navigationItems.map((item) => (
                  <AppSidebarLink key={item.id} item={item} />
                ))}
              </SidebarMenu>
            </nav>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </Sidebar>
  )
}
```

- `end` on `/` so "Dashboard" is not active on every page; nested paths (e.g. `/tickets/42` later) keep their item active.
- On phones the sidebar is a `Sheet`; clicking a link closes it.

**Create file: `client/src/components/layout/AppHeader.tsx`**

```tsx
import { LogOutIcon } from 'lucide-react'
import { signOut } from '@/auth/sign-in'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { authMessages } from '@/features/auth/auth-messages'
import { useCurrentUser } from '@/features/auth/useCurrentUser'

/** Top bar: sidebar toggle, signed-in user, sign out. Sign out clears the token; RequireAuth then redirects to /login. */
export function AppHeader() {
  const { data: user } = useCurrentUser()

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      <SidebarTrigger className="-ms-1" />
      <Separator orientation="vertical" className="me-2 data-[orientation=vertical]:h-4" />
      <div className="ms-auto flex items-center gap-3">
        {user ? <span className="text-sm text-muted-foreground">{user.fullName}</span> : null}
        <Button variant="outline" size="sm" onClick={signOut}>
          <LogOutIcon aria-hidden="true" />
          {authMessages.signOut}
        </Button>
      </div>
    </header>
  )
}
```

**Create file: `client/src/components/layout/AppLayout.tsx`**

```tsx
import { Outlet } from 'react-router'
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar'
import { AppHeader } from './AppHeader'
import { AppSidebar } from './AppSidebar'

/** App shell for signed-in pages: sidebar + header + the current page. */
export function AppLayout() {
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <AppHeader />
        <div className="flex flex-1 flex-col p-4 md:p-6">
          <Outlet />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
```

(`SidebarInset` renders the `<main>` element.)

### 8 — Routes, protected routes, App root, toaster (Green, part 5)

**Create file: `client/src/app/RequireAuth.tsx`**

```tsx
import { Navigate, Outlet, useLocation } from 'react-router'
import { useIsAuthenticated } from '@/auth/useIsAuthenticated'
import type { LoginRedirectState } from './return-path'

/** Renders the child routes only when signed in; otherwise redirects to /login and remembers the page. */
export function RequireAuth() {
  const isAuthenticated = useIsAuthenticated()
  const location = useLocation()

  if (!isAuthenticated) {
    const state: LoginRedirectState = { from: location.pathname + location.search }
    return <Navigate to="/login" replace state={state} />
  }

  return <Outlet />
}
```

**Create file: `client/src/app/AppRoutes.tsx`**

```tsx
import { Navigate, Route, Routes } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { LoginPage } from '@/pages/auth/LoginPage'
import { ComingSoonPage } from '@/pages/coming-soon/ComingSoonPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { RequireAuth } from './RequireAuth'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          {/* Areas built by later stories: each story replaces its line with the real page routes. */}
          <Route path="tickets" element={<ComingSoonPage area="tickets" />} />
          <Route path="customers" element={<ComingSoonPage area="customers" />} />
          <Route path="knowledge-base" element={<ComingSoonPage area="knowledgeBase" />} />
          <Route path="reports" element={<ComingSoonPage area="reports" />} />
          <Route path="users" element={<ComingSoonPage area="users" />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
```

**File: `client/src/App.tsx`** — replace the whole file:

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { BrowserRouter } from 'react-router'
import { AppRoutes } from '@/app/AppRoutes'
import { createQueryClient } from '@/app/query-client'
import { getAccessToken, subscribeToSession } from '@/auth/session'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'

function App() {
  const [queryClient] = useState(createQueryClient)

  // Signed out (button, expired or rejected token): forget every cached server response,
  // so the next user never sees the previous user's data.
  useEffect(
    () =>
      subscribeToSession(() => {
        if (getAccessToken() === null) queryClient.clear()
      }),
    [queryClient],
  )

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
      <ApiErrorToaster />
    </QueryClientProvider>
  )
}

export default App
```

`client/src/main.tsx` is **unchanged** (`StrictMode`, `import './index.css'`, `App`).

**File: `client/src/components/ApiErrorToaster.tsx`** — replace line 2 only (CRM-5 contract):

```tsx
import { toast } from 'sonner'
import { Toaster } from '@/components/ui/sonner'
```

Everything else in the file (the 401 early return, `<Toaster position="top-center" closeButton />`) stays. **Do not** pass color props (`richColors`); the wrapper maps toast colors to `--popover`, `--popover-foreground`, `--border`, `--radius`.

Run `npm test` → **Green**: **84 passed** in 11 files (4 `App.auth.test.tsx`, 10 `App.layout.test.tsx`, 1 `App.toast.test.tsx`, 39 `theme.test.ts`, 10 `app/return-path.test.ts`, 3 `pages/dashboard/DashboardPage.test.tsx`, 4 `api/client.auth.test.ts`, 3 `api/client.test.ts`, 2 `api/health.test.ts`, 4 `auth/session.test.ts`, 4 `components/ApiErrorToaster.test.tsx`).

### 9 — How later stories build on this (write nothing here; for later planners)

- **New feature page (CRM-8 customers, CRM-12 tickets, …):** create `client/src/pages/<area>/<Name>Page.tsx`, replace the area's `ComingSoonPage` line in `client/src/app/AppRoutes.tsx` with the real route(s) (nested routes like `customers/:id` go next to it inside the `AppLayout` route), and keep the item in `client/src/app/navigation.ts`. Server data uses React Query (`useQuery` with `({ signal }) => apiGet(...)` through a typed function in `client/src/api/<resource>.ts`); mutations invalidate their query keys. Remove `ComingSoonPage` when no route uses it. The "every navigation item opens a page" test keeps dead links out; update its expectations (heading / "coming soon" text) when an area gets a real page.
- **New sidebar area:** add an entry to `navigationItems`, a label to `shellMessages.nav`, a route in `AppRoutes.tsx`, and the label to `NAVIGATION_LABELS` in `App.layout.test.tsx`.
- **New shadcn components:** `npx shadcn@4.21.2 add <name> -y` (same pinned version; `components.json` already has `rtl: true`). Never hand-edit `client/src/components/ui/`.
- **CRM-4 (ar/en + RTL):** move `client/src/app/messages.ts`, `client/src/features/auth/auth-messages.ts` and `client/src/api/error-messages.ts` into `client/src/i18n/{en,ar}.json`; `NavigationItem.id` becomes the translation key (`nav.<id>`). `loginSchema` is built at module load with English messages — CRM-4 turns it into a factory taking the translation function (or uses message keys and translates in `FieldError`). Set `<html lang dir>`, pass `dir` and `side={dir === 'rtl' ? 'right' : 'left'}` to `<Sidebar>` (shadcn `Sidebar` accepts `side` and `dir`; see https://ui.shadcn.com/docs/rtl/vite), and pick a font with Arabic glyphs (Geist has none). shadcn's own sr-only texts ("Toggle Sidebar", "Sidebar", "Displays the mobile sidebar.") are hard-coded English inside `components/ui/sidebar.tsx`; CRM-4 decides how to translate them (e.g. `aria-label` on `SidebarTrigger`).
- **CRM-7 (roles & permissions):** add an optional `permission` (or `roles`) field to `NavigationItem`, filter `navigationItems` in `AppSidebar` with the current user's permissions (from `useCurrentUser()` or a permissions query), and wrap protected routes in a permission guard next to `RequireAuth` (UI hiding only; the API still enforces authorization).
- **CRM-6 (users):** replaces the `users` coming-soon route with the user-management pages.
- **Dark mode / branding:** `.dark` variables already exist; a theme toggle adds `next-themes`' `ThemeProvider` (`attribute="class"`) around `App`. Branding overrides the `:root` variables (e.g. `--primary`, `--sidebar-primary`) — no component change.
- **Bundle size:** when real pages arrive, load them with `React.lazy` + `Suspense` per route (vercel rule `bundle-dynamic-imports`) to clear the Vite 500 kB chunk warning.
- **Tests of signed-in pages** use `saveSession('good-token', inOneHour())` + `vi.stubGlobal('fetch', fakeApi())` from `client/src/test/fake-api.ts` (extend `fakeApi` with new endpoints) or render the page inside `QueryClientProvider client={createQueryClient()}` with mocked `client/src/api/*` modules, like `DashboardPage.test.tsx`.

---

## Backend Tasks

No backend changes required. `dotnet build` and `dotnet test` must stay green (40 tests).

---

## Edge Cases & Failure Modes

- **Signed out, any protected URL (e.g. `/customers?x=1`)** → `RequireAuth` redirects to `/login` with `state.from = "/customers?x=1"`; after login `LoginPage` navigates there (`getReturnPath`). Covered by `redirects / to /login when signed out`, `opens the page the user asked for after login`.
- **Open redirect via router state** → `getReturnPath` accepts only paths starting with a single `/` (rejects `//host`, `/\host`, `https://…`) and never returns `/login` (no loop). Covered by `return-path.test.ts`.
- **Visiting `/login` while signed in** → `LoginPage` renders `<Navigate>` to the return path (default `/`). Covered.
- **Unknown path** → `path="*"` redirects to `/` (then to `/login` when signed out). Covered by `redirects an unknown path to the dashboard`.
- **Stored token rejected by the API (expired on server, revoked)** → `/api/auth/me` 401 → `client.ts` lines 59–61 `clearSession()` → `useIsAuthenticated` false → `RequireAuth` redirects to `/login`; no toast (401 rule in `ApiErrorToaster.tsx`). Covered by `returns to /login when the stored token is rejected`.
- **Token expires while the tab stays open without any request** → `getAccessToken()` returns `null` only when something re-reads it (next render or request). The redirect happens on the next navigation/re-render or the next API call (401). Accepted for this story; a timer-based sign-out is out of scope.
- **Sign out, then another user signs in** → `App` clears the React Query cache on every session change to "signed out", so `/me` is fetched again and the previous name never shows. Covered by `loads the current user again after signing out and back in`.
- **Sign out in another tab** → the `storage` event (`session.ts` lines 61–72) re-renders this tab; `RequireAuth` redirects to `/login` and the cache is cleared.
- **Failed queries** → `retry: false` (one toast per failure, CRM-5 behaviour). The dashboard shows "unavailable" when health fails. Covered by `App.toast.test.tsx` and `DashboardPage.test.tsx`.
- **StrictMode double effects** → React Query aborts the first `/me` / health request through `signal`; aborted requests never toast (CRM-5).
- **Login form: empty / invalid email / empty password** → zod messages inline, no API call (`noValidate` disables browser bubbles). Server-side 400 (e.g. email accepted by zod but rejected by FluentValidation) → generic CRM-5 toast "The request is invalid. Check the entered data." (unchanged).
- **Login form: double submit** → button disabled while `isSubmitting`.
- **Login 5xx / network error** → toast from `ApiErrorToaster`; no inline message (only 401 is inline).
- **Narrow screens (< 768 px)** → `useIsMobile()` turns the sidebar into a `Sheet` opened by `SidebarTrigger`; links close it (`setOpenMobile(false)`).
- **jsdom has no `matchMedia`** → stubbed in `client/src/test/setup.ts` (`matches: false` = desktop + light theme). Removing it fails 19 tests.
- **Generated shadcn files contain physical classes** (`sidebar.tsx`: `left-0`, `right-0`, `side="left"`) → excluded from the logical-class guard (`./components/ui/`); they are RTL-aware via `rtl:` variants and the `side`/`dir` props CRM-4 sets.
- **Someone adds a hex color or `ml-`/`left-` class in app code** → `theme.test.ts` lists the offending file.
- **Someone adds a sidebar item without a route** → it falls to `path="*"` → dashboard; `opens a page for every navigation item` fails because the expected heading never appears (after its label is added to `NAVIGATION_LABELS`; the first navigation test fails first because the label list differs).
- **`shadcn` registry unreachable / CLI version drift** → the CLI is pinned to `4.21.2`; if the registry is down the commands fail with a network error — retry, do not hand-write components. If the generated `index.css` lacks any variable listed in `THEME_COLORS`, `theme.test.ts` fails.
- **`npm audit` high advisories** → inside the `shadcn` CLI tree only (devDependency). Do not `npm audit fix --force` (it downgrades/upgrades majors).
- **Vite chunk-size warning (577 kB)** → expected; not an error.

---

## Test Plan

1. **Component (app level, new)** — `client/src/App.layout.test.tsx`: `redirects / to /login when signed out` (AC 1), `lands on the dashboard with the sidebar navigation after a successful login` (AC 2), `shows the signed-in user in the header`, `signs out: clears the token and returns to /login` (AC 3), `loads the current user again after signing out and back in`, `returns to /login when the stored token is rejected`, `opens the page the user asked for after login`, `redirects /login to the dashboard when already signed in`, `redirects an unknown path to the dashboard`, `opens a page for every navigation item (areas not built yet say "coming soon")`.
2. **Component (app level, rewritten)** — `client/src/App.auth.test.tsx`: `shows the sign-in form with the app name when signed out`, `shows an inline error and no toast for a wrong password` (CRM-2 rule), `shows field errors and does not call the API when the fields are empty`, `shows a field error for an invalid email address`.
3. **Component (app level, rewritten)** — `client/src/App.toast.test.tsx`: `shows an error toast when the health call fails` (now on the dashboard, signed in).
4. **Component (new)** — `client/src/pages/dashboard/DashboardPage.test.tsx`: `welcomes the signed-in user`, `shows "ok" when the API is healthy`, `shows "unavailable" when the API call fails` (moved from the deleted `client/src/App.test.tsx`).
5. **Unit (new)** — `client/src/app/return-path.test.ts`: 10 cases of `getReturnPath`.
6. **Guard (new)** — `client/src/theme.test.ts`: 18 × `defines --<color> as a CSS variable for light and dark mode` and 18 × `maps the Tailwind color "<color>" to its CSS variable` (AC 4), `scans the app sources`, `uses no hard-coded hex colors in source files`, `uses logical (RTL-safe) direction classes in app components`.
7. **Removed** — `client/src/App.test.tsx` (content moved to item 4).
8. **Unchanged, must stay green** — `client/src/api/client.test.ts`, `client/src/api/client.auth.test.ts`, `client/src/api/health.test.ts`, `client/src/auth/session.test.ts`, `client/src/components/ApiErrorToaster.test.tsx` (now renders the shadcn `Toaster` wrapper; needs the `matchMedia` stub).
9. **Backend (unchanged)** — `dotnet test` 40 passed.
10. **Manual smoke** — Verification step 6.

---

## Verification Steps

1. **Frontend tests:** in `client/` run `npm test` — **84 passed** in 11 files, process exits.
2. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed (the "chunks larger than 500 kB" message is a warning).
3. **Frontend lint:** in `client/` run `npm run lint` — exit code 0, no warnings, no errors.
4. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings.
5. **Backend tests:** in `server/` run `dotnet test` — **40 passed** (12 unit, 28 integration).
6. **Manual smoke** (user-secrets from CRM-2 already set):
   - Terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http`.
   - Terminal 2: `cd client && npm run dev`, open `http://localhost:5173/` → URL becomes `/login`, centered card "Customer Support CRM" with Email / Password / "Sign in".
   - Click "Sign in" with empty fields → "Enter your email." / "Enter your password." under the fields. Wrong password → "Invalid email or password.", **no toast**.
   - Correct password (`admin@crm.local` + seed password) → URL `/`, sidebar with Dashboard (active), Tickets, Customers, Knowledge base, Reports, Users; header with "System Administrator" and "Sign out"; dashboard "Welcome, System Administrator" and "API status: ok".
   - Click "Customers" → "Customers" + "This area is coming soon."; the item is highlighted. Click the sidebar toggle → sidebar collapses; narrow the window below 768 px → sidebar opens as a sheet from the toggle.
   - Reload on `/customers` → still signed in, same page. "Sign out" → `/login`; DevTools → Application → Local Storage: `crm.session` is gone.
   - Open `http://localhost:5173/tickets` while signed out → `/login`; sign in → `/tickets`.
   - Stop the API and reload the dashboard while signed in → toast "Cannot reach the server…" and "API status: unavailable".
7. **Regression:** `git status` shows no changes under `server/`, `.claude/`, `.mcp.json`, `CLAUDE.md`. `git grep -nE "#[0-9a-fA-F]{6}\b" -- client/src` finds nothing. `client/src/api/client.ts` is still the only `fetch` caller (`git grep -n "fetch(" -- client/src ':!*.test.*' ':!client/src/test'` → only `client/src/api/client.ts`).

---

## Done Criteria

- [ ] Visiting `/` while logged out redirects to `/login` (`redirects / to /login when signed out` green).
- [ ] After a successful login the user lands on the dashboard and the sidebar shows the navigation items (`lands on the dashboard with the sidebar navigation after a successful login` green).
- [ ] Logout clears the token and returns to `/login` (`signs out: clears the token and returns to /login` green).
- [ ] Theme colors are CSS variables in `client/src/index.css` (`:root` + `.dark`, mapped in `@theme inline`); `theme.test.ts` green, no hex colors in source files.
- [ ] Every sidebar item opens an existing route; unbuilt areas show `ComingSoonPage` (test green).
- [ ] Login page uses shadcn `Card`/`Field`/`Input`/`Button` with react-hook-form + zod; 401 inline, never a toast.
- [ ] Tailwind v4 + shadcn/ui (`components.json`, `radix-nova`, `rtl: true`) set up with the pinned CLI commands; `ApiErrorToaster` uses `@/components/ui/sonner` (one import changed); `sonner` not reinstalled.
- [ ] `react-router`, `@tanstack/react-query`, `react-hook-form`, `zod`, `@hookform/resolvers` added at the versions above; `shadcn`, `tailwindcss`, `@tailwindcss/vite` are devDependencies.
- [ ] App components use only logical direction classes; all user-facing strings are in `client/src/app/messages.ts` or `client/src/features/auth/auth-messages.ts`.
- [ ] `CurrentUserPanel.tsx` and `App.test.tsx` deleted; no file in `client/src/components/ui/` edited by hand.
- [ ] `npm test` (84), `npm run build`, `npm run lint`, `dotnet build`, `dotnet test` (40) all pass.
- [ ] Committed on `feature/crm-3-app-layout` with message `CRM-3: app layout with shadcn/ui, routing and login page`.
- [ ] Overview `00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 05.**
