# Story 05 — Arabic / English with RTL support (Story: CRM-4)

## Prerequisites

- Story 01 completed: [01-story-project-skeleton.md](01-story-project-skeleton.md) (CRM-1) — `server/` solution, `client/` Vite + React + Vitest + oxlint. Merged to `main`.
- Story 02 completed: [02-story-global-error-handling.md](02-story-global-error-handling.md) (CRM-5) — `GlobalExceptionHandler`, `AddCrmErrorHandling` / `UseCrmErrorHandling`, FluentValidation + `ValidateOrThrowAsync`, `CrmApiFactory`, client `ApiError` / `onApiError` / `ApiErrorToaster` / `error-messages.ts`. Its section "7 — How CRM-3 builds on this" is **binding** for this story: CRM-4 replaces the strings of `client/src/api/error-messages.ts` with i18n keys and passes `dir` from the current language to the `Toaster`. Merged to `main`.
- Story 03 completed: [03-story-authentication.md](03-story-authentication.md) (CRM-2) — `AuthService`, `LoginRequestValidator`, 401 never toasts. Its section "6 — How later stories build on this" is **binding**: CRM-4 moves `auth-messages.ts` into `client/src/i18n/{en,ar}.json`. Merged to `main`.
- Story 04 completed: [04-story-app-layout.md](04-story-app-layout.md) (CRM-3) — shadcn/ui (`rtl: true`, CLI pinned to **`shadcn@4.21.2`**), routes, sidebar, header, login page, React Query, `theme.test.ts` guards. Its section "9 — How later stories build on this" (CRM-4 bullet) is **binding**: move `app/messages.ts`, `features/auth/auth-messages.ts` and `api/error-messages.ts` strings into `client/src/i18n/{en,ar}.json`; `NavigationItem.id` becomes the translation key (`nav.<id>`); make `loginSchema` translatable (factory taking `t`); set `<html lang dir>`; pass `dir` and `side={dir === 'rtl' ? 'right' : 'left'}` to `<Sidebar>`; pick a font with Arabic glyphs (Geist has none); decide how shadcn's hard-coded sr-only texts are translated. Merged to `main`.
- Work on branch **`feature/crm-4-i18n-rtl`** (already created from `main`).
- Phase 1 order: CRM-1 ✅ → CRM-5 ✅ → CRM-2 ✅ → CRM-3 ✅ → **CRM-4 (this)** → CRM-6 (users) → CRM-7 (roles & permissions) → customers → tickets → SLA → email/WhatsApp.
- **Network access** is required once: `npm install` and `npx shadcn@4.21.2 add direction` (reads the shadcn registry).
- **Shared contract created here** (every later story depends on it): the translation files `client/src/i18n/{en,ar}.json` with typed keys (`client/src/i18n/i18next.d.ts`), the i18n instance + `getLanguage()` / `setLanguage()` in `client/src/i18n/i18n.ts` (storage key `crm.language`), the `Accept-Language` header sent by `client/src/api/client.ts`, the guard tests `client/src/i18n/translations.test.ts` and `client/src/no-hardcoded-text.test.ts`; on the server the request localization (`AddCrmLocalization` / `UseCrmLocalization`, middleware **before** `UseCrmErrorHandling`), `LocalizedText.Get(english, arabic)` and the convention "user-facing server text lives in a static `<Feature>Text` class in `Crm.Application`" guarded by `LocalizedTextCatalogTests`.

---

## Story Goal

Users switch the CRM between **Arabic** and **English**; the layout direction, every UI string and the API's validation messages follow the chosen language.

1. A language switch ("العربية" in English, "English" in Arabic) is shown on the **login page** and in the **header**. Arabic sets **`<html dir="rtl" lang="ar">`**, English sets **`<html dir="ltr" lang="en">`** (AC 1). The sidebar moves to the right in Arabic (desktop and the mobile sheet), Radix primitives get the direction through shadcn's `DirectionProvider`, and toasts use the same direction.
2. **All UI strings come from `client/src/i18n/{en,ar}.json`** (AC 2): the temporary modules `app/messages.ts` and `features/auth/auth-messages.ts` are deleted, `api/error-messages.ts` keeps its functions but returns `i18n.t(...)`, the zod login messages are built from `t`, and the English sr-only text of shadcn's `SidebarTrigger` is replaced by a translated `aria-label`. A guard test fails when a component contains text written directly in JSX / `aria-label` / `title` / `placeholder` / `alt`, or when any English UI string from `en.json` is copied as a string literal into app code. Translation keys are **typed**: a misspelled key fails `npm run build`.
3. **The chosen language survives a reload** (AC 3): stored in `localStorage` under `crm.language`, read at start-up; anything else (missing, unsupported, storage blocked) → English.
4. **API messages follow `Accept-Language`** (AC 4): the client sends `Accept-Language: en|ar` on every call; the server sets the request UI culture (`ar`, `ar-SA`, `ar-EG,…` → Arabic; anything else → English) and returns FluentValidation messages (with Arabic field names), ProblemDetails titles and the wrong-credentials detail in that language, plus a `Content-Language` response header.
5. Arabic text renders with **Noto Sans Arabic** (Latin text keeps Geist).

**Not in scope:** languages other than ar/en; storing the language per user on the server; translating user data or email/WhatsApp templates; localized date/number formatting (no dates are shown yet); the mobile sidebar sheet's sr-only title/description and the sheet "Close" text inside generated `components/ui/` files (see Edge Cases); dark mode / branding.

---

## Context — Read These Files First

1. `CLAUDE.md` — **Frontend rules**: "No hard-coded user-facing text: all strings in i18n files (`ar` and `en`)", logical Tailwind classes, colors only via theme variables, API calls only through `client/src/api`, tests by role/label, `vercel-react-best-practices`. **Architecture decisions → Frontend** (binding): "i18n: `react-i18next` (`client/src/i18n/{ar,en}.json`)". **Backend rules**: layers (Application must not reference ASP.NET Core), ProblemDetails, async.
2. `.squad/stories/foundation/CRM-4/intake.md` — acceptance criteria 1–4 and **Out of scope**.
3. [04-story-app-layout.md](04-story-app-layout.md) lines 1286–1296 — "How later stories build on this" (the CRM-4 bullet is line 1291). [02-story-global-error-handling.md](02-story-global-error-handling.md) line 1147 (error messages → i18n keys, `dir` on the `Toaster`). [03-story-authentication.md](03-story-authentication.md) line 1836.
4. `client/src/app/messages.ts` (21 lines, **deleted**) — `shellMessages` (lines 3–19) and `NavigationId` (line 21; moves to `navigation.ts`). `client/src/features/auth/auth-messages.ts` (14 lines, **deleted**). `client/src/api/error-messages.ts` (34 lines, **replaced**: lines 5–13 `messages` object become JSON keys; `getApiErrorMessage` lines 15–30 and `getApiErrorDescription` lines 32–34 keep their signatures).
5. `client/src/app/navigation.ts` — line 10 imports `NavigationId` from `./messages`; lines 12–18 `NavigationItem`; lines 21–28 `navigationItems` (unchanged list).
6. `client/src/components/layout/AppSidebar.tsx` — line 27 `shellMessages.nav[item.id]`, line 36 `<Sidebar>` (no `side`/`dir` yet), lines 38 and 43 app name and nav label.
7. `client/src/components/layout/AppHeader.tsx` — line 15 `<SidebarTrigger className="-ms-1" />`, line 21 `authMessages.signOut`.
8. `client/src/components/ui/sidebar.tsx` (generated — **do not edit**) — lines 149–161 `Sidebar` props `side?: "left" | "right"` and `dir`; line 183 passes `dir` to the mobile `SheetContent`, line 193 `side`; lines 195–198 hard-coded sr-only "Sidebar" / "Displays the mobile sidebar."; lines 251–275 `SidebarTrigger` spreads `...props` onto `Button` (line 269) and renders `<span className="sr-only">Toggle Sidebar</span>` (line 272) — an `aria-label` prop therefore becomes the accessible name.
9. `client/src/components/ui/sonner.tsx` (generated — **do not edit**) — lines 37–42: the wrapper sets `toastOptions={{ classNames: { toast: "cn-toast" } }}` and then spreads `{...props}` (line 42), so passing `toastOptions` **replaces** it (repeat `cn-toast`).
10. `client/src/components/ApiErrorToaster.tsx` — lines 13–22 the listener (unchanged, 401 early return line 18 **stays**), line 24 `<Toaster position="top-center" closeButton />`.
11. `client/src/features/auth/LoginForm.tsx` — line 10 imports `loginSchema`; lines 14 and 26 `serverError` string state; line 16 `zodResolver(loginSchema)`; all labels through `authMessages`. `client/src/features/auth/login-schema.ts` lines 5–8 (module-level schema with English messages).
12. `client/src/pages/auth/LoginPage.tsx` lines 18–28 (card header: title + description), `client/src/pages/dashboard/DashboardPage.tsx` lines 12–14 (`apiStatus`) and 19–24, `client/src/pages/coming-soon/ComingSoonPage.tsx` lines 1 and 7–8.
13. `client/src/api/client.ts` — line 1 imports; line 40 `const headers: Record<string, string> = { Accept: 'application/json' }` (add `Accept-Language`). Still the only `fetch` caller.
14. `client/src/App.tsx` (32 lines) — lines 22–29 provider tree; `client/src/main.tsx` lines 3–4 imports.
15. `client/src/test/setup.ts` — lines 22–27 `afterEach` (add the language reset; it becomes `async`).
16. `client/src/index.css` — line 4 `@import "@fontsource-variable/geist";`, line 10 `--font-sans: 'Geist Variable', sans-serif;`. `client/index.html` line 2 `<html lang="en">`.
17. `client/src/theme.test.ts` — lines 10–14 the source glob pattern (reused by the new guard), lines 16–21 `block()`; you add one test before line 54 (`scans the app sources`).
18. `client/src/App.layout.test.tsx`, `client/src/App.auth.test.tsx`, `client/src/components/ApiErrorToaster.test.tsx`, `client/src/pages/dashboard/DashboardPage.test.tsx` — English expectations; they must stay green **unchanged** (English is the default and `setup.ts` resets to English after every test).
19. `client/.oxlintrc.json` lines 8–16 — the `src/components/ui/**` override already covers the new generated `direction.tsx` (it exports `useDirection`).
20. `server/src/Crm.Api/Program.cs` (38 lines) — lines 10–14 service registration, line 18 `app.UseCrmErrorHandling();` (localization goes **before** it — see "Verified while planning").
21. `server/src/Crm.Api/ErrorHandling/GlobalExceptionHandler.cs` — lines 50–92 `ToProblemDetails`: seven hard-coded English `Title`s (lines 56, 61, 66, 72, 78, 84, 90).
22. `server/src/Crm.Api/ErrorHandling/ErrorHandlingExtensions.cs` (29 lines) — lines 10–15 `CustomizeProblemDetails` (add titles for status-code-pages responses), lines 22–28 `UseCrmErrorHandling`.
23. `server/src/Crm.Application/Auth/LoginRequestValidator.cs` lines 9–10; `server/src/Crm.Infrastructure/Identity/AuthService.cs` lines 14–15 `InvalidCredentialsMessage` const (used on lines 24 and 30 only — verified with grep).
24. `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs` lines 5–9 — Application must not reference `Microsoft.AspNetCore*`; the new `LocalizedText` uses only `System.Globalization`.
25. `server/tests/Crm.Api.IntegrationTests/Auth/LoginTests.cs` lines 54 and 67 assert the English detail "Invalid email or password." without `Accept-Language` — must stay green (default English). `server/tests/Crm.Api.IntegrationTests/Infrastructure/TestEndpointsStartupFilter.cs` lines 33–43: `/_test/errors/not-found` etc. (reused by the new tests).

Verified while planning (fresh scratch clone of `main` in the session scratchpad; every file and command below was replayed from scratch; **`npm test` 213 passed in 16 files, `npm run build` and `npm run lint` clean (exit 0), `dotnet build` 0 warnings / 0 errors, `dotnet test` 73 passed (31 unit + 42 integration)**):

- npm (latest on 2026-10-06): **i18next 26.4.2** (peer `typescript ^5 || ^6 || ^7`), **react-i18next 17.0.15** (peer `react >=16.8`, `i18next >=26.2.0`), **@fontsource-variable/noto-sans-arabic 5.3.0** (CSS family name `'Noto Sans Arabic Variable'`; the font shadcn's RTL guide for Vite recommends). No language-detector package: the stored choice is read by 10 lines of our own code.
- `npx shadcn@4.21.2 add direction -y` creates only `src/components/ui/direction.tsx` (`DirectionProvider` over `radix-ui`'s `Direction`, props `dir` or `direction`, plus `useDirection`). No new dependency.
- i18next 26: `createInstance()`, `initAsync: false` (synchronous init with bundled resources), `supportedLngs` accepts the `as const` tuple, `i18n.dir('ar') === 'rtl'`, and `languageChanged` **fires during `init`**, so a listener registered before `init()` also sets `<html lang dir>` for the initial language.
- Typed keys via `CustomTypeOptions` (`resources: { translation: typeof en }`, JSON import works with the existing `moduleResolution: "bundler"`): `i18n.t('auth.typo')` fails `tsc -b` with `TS2345`; template keys like ``t(`nav.${item.id}`)`` type-check because `NavigationId = keyof (typeof en)['nav']`.
- react-hook-form picks up the new resolver after `t` changes (`useMemo(() => createLoginSchema(t), [t])`): submitting the empty form after switching shows the Arabic messages (test below).
- `vi.resetModules()` + `await import('./i18n')` gives a fresh i18next instance (our own `createInstance()`; a reset would not re-create i18next's default singleton) — that is how the reload test works.
- sonner 2.0.8 `ToasterProps` has `dir?: 'rtl' | 'ltr' | 'auto'` and `containerAriaLabel` (default "Notifications"); the close button label is `toastOptions.closeButtonAriaLabel` (default "Close toast").
- FluentValidation 12.1.1 ships Arabic messages and uses `CultureInfo.CurrentUICulture` (`ar` and `ar-SA` both work): `'البريد الإلكتروني' لا يجب أن يكون فارغاً.` — `.WithName(_ => AuthText.EmailField)` (lazy) translates the field name per request. An empty email fails **both** `NotEmpty` and `EmailAddress`, so the tests use `Assert.Contains`, not `Assert.Equal` on the whole list.
- **Middleware order matters (probed):** with `UseCrmLocalization()` **after** `UseCrmErrorHandling()`, the validation messages are Arabic but every ProblemDetails `title` stays English (6 tests fail) — the culture set inside the pipeline does not flow back out to the exception handler / status-code pages. With localization **first**, all 42 integration tests pass.
- Only the **UI culture** follows the request (`SupportedCultures = [en]`, `SupportedUICultures = [en, ar]`): the formatting culture stays `en`, so nothing is ever formatted with the Arabic (Um Al-Qura/Hijri) calendar by accident. `ApplyCurrentCultureToResponseHeaders = true` writes `Content-Language: ar|en`.
- `npm run build` still prints the Vite "chunks larger than 500 kB" **warning** (631 kB, 200 kB gzip; was 577 kB) and now emits the Noto Sans Arabic `.woff2` subsets (the Arabic subset is ~166 kB, loaded by the browser only when Arabic glyphs are drawn).

---

## Product rules (from story)

| | Before (CRM-3) | After (CRM-4) |
|---|---|---|
| UI language | English only, strings in `messages.ts` / `auth-messages.ts` / `error-messages.ts` | English (default) or Arabic, strings only in `client/src/i18n/{en,ar}.json` |
| `<html>` | `lang="en"`, no `dir` | `lang`/`dir` follow the language (`en`/`ltr`, `ar`/`rtl`) |
| Sidebar | always left | left in English, right in Arabic |
| Language choice | — | switch on login page + header; stored in `localStorage["crm.language"]` |
| API messages | English only | `Accept-Language` decides (ar / en, default en); client always sends it |

---

## Frontend Tasks

All commands run from `client/`. Follow `vercel-react-best-practices` (direct imports, no barrel files). New files import with the `@/` alias; existing files keep their import style.

### 1 — Tests first (Red)

**Create file: `client/src/App.i18n.test.tsx`** — AC 1, 2, 3 through the real `App`, real API client and the fake `fetch` from CRM-3.

```tsx
import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'
import { ADMIN_PASSWORD, fakeApi, inOneHour } from './test/fake-api'

const ARABIC_NAVIGATION_LABELS = ['لوحة التحكم', 'التذاكر', 'العملاء', 'قاعدة المعرفة', 'التقارير', 'المستخدمون']

function html() {
  return document.documentElement
}

async function renderLoginPage() {
  vi.stubGlobal('fetch', fakeApi())
  render(<App />)
  await screen.findByRole('form', { name: 'Sign in' })
}

describe('Language switching (ar / en)', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('starts in English with <html dir="ltr" lang="en">', async () => {
    await renderLoginPage()

    expect(html()).toHaveAttribute('lang', 'en')
    expect(html()).toHaveAttribute('dir', 'ltr')
  })

  it('switching to Arabic sets <html dir="rtl" lang="ar"> and translates the page', async () => {
    await renderLoginPage()

    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))

    expect(await screen.findByRole('form', { name: 'تسجيل الدخول' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'نظام إدارة دعم العملاء' })).toBeInTheDocument()
    expect(html()).toHaveAttribute('lang', 'ar')
    expect(html()).toHaveAttribute('dir', 'rtl')
  })

  it('switching back to English sets <html dir="ltr" lang="en">', async () => {
    await renderLoginPage()
    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))
    await screen.findByRole('form', { name: 'تسجيل الدخول' })

    fireEvent.click(screen.getByRole('button', { name: 'English' }))

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(html()).toHaveAttribute('lang', 'en')
    expect(html()).toHaveAttribute('dir', 'ltr')
  })

  it('shows no English text on the Arabic login page, field errors included', async () => {
    await renderLoginPage()
    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))
    await screen.findByRole('form', { name: 'تسجيل الدخول' })

    fireEvent.click(screen.getByRole('button', { name: 'تسجيل الدخول' }))

    expect(await screen.findByText('أدخل بريدك الإلكتروني.')).toBeInTheDocument()
    expect(screen.getByText('أدخل كلمة المرور.')).toBeInTheDocument()
    // The only Latin word left is the switch back to English.
    expect(document.body.textContent?.match(/[A-Za-z]+/g)).toEqual(['English'])
  })

  it('remembers the chosen language for the next visit', async () => {
    await renderLoginPage()

    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))
    await screen.findByRole('form', { name: 'تسجيل الدخول' })

    expect(localStorage.getItem('crm.language')).toBe('ar')
  })

  it('signed in, Arabic: sidebar on the right, navigation and header in Arabic', async () => {
    saveSession('good-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)
    await screen.findByRole('heading', { level: 1, name: 'Dashboard' })

    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'لوحة التحكم' })).toBeInTheDocument()
    const navigation = screen.getByRole('navigation', { name: 'التنقل الرئيسي' })
    expect(within(navigation).getAllByRole('link').map((link) => link.textContent)).toEqual(ARABIC_NAVIGATION_LABELS)
    expect(screen.getByRole('button', { name: 'تسجيل الخروج' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'إظهار القائمة الجانبية أو إخفاؤها' })).toBeInTheDocument()
    // shadcn Sidebar: no role exposes its side, so the data attribute is the observable result.
    expect(document.querySelector('[data-slot="sidebar"][data-side]')).toHaveAttribute('data-side', 'right')
  })

  it('signs in in Arabic and shows API error toasts in Arabic', async () => {
    vi.stubGlobal('fetch', fakeApi({ healthStatus: 500 }))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'العربية' }))
    await screen.findByRole('form', { name: 'تسجيل الدخول' })

    fireEvent.change(screen.getByLabelText('البريد الإلكتروني'), { target: { value: 'admin@crm.local' } })
    fireEvent.change(screen.getByLabelText('كلمة المرور'), { target: { value: ADMIN_PASSWORD } })
    fireEvent.click(screen.getByRole('button', { name: 'تسجيل الدخول' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'لوحة التحكم' })).toBeInTheDocument()
    expect(await screen.findByText('حدث خطأ ما. حاول مرة أخرى.')).toBeInTheDocument()
    expect(screen.getByText('المرجع: health-1')).toBeInTheDocument()
  })
})
```

**Create file: `client/src/i18n/i18n.test.ts`** — AC 3 (reload = fresh module) and the `<html>` attributes at start-up.

```ts
import { beforeEach, describe, expect, it, vi } from 'vitest'

// Every test loads a fresh copy of the module, like a page reload: the language is read from storage at start-up.
async function loadI18n() {
  vi.resetModules()
  return import('./i18n')
}

describe('i18n start-up and language persistence', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('starts in English with <html dir="ltr" lang="en"> when nothing is saved', async () => {
    const { i18n } = await loadI18n()

    expect(i18n.language).toBe('en')
    expect(document.documentElement).toHaveAttribute('lang', 'en')
    expect(document.documentElement).toHaveAttribute('dir', 'ltr')
  })

  it('starts in the saved language after a reload', async () => {
    localStorage.setItem('crm.language', 'ar')

    const { i18n } = await loadI18n()

    expect(i18n.language).toBe('ar')
    expect(i18n.t('auth.signIn')).toBe('تسجيل الدخول')
    expect(document.documentElement).toHaveAttribute('lang', 'ar')
    expect(document.documentElement).toHaveAttribute('dir', 'rtl')
  })

  it.each(['fr', '', 'AR', '{"x":1}'])('ignores an unsupported saved value %j and starts in English', async (saved) => {
    localStorage.setItem('crm.language', saved)

    const { i18n } = await loadI18n()

    expect(i18n.language).toBe('en')
  })

  it('setLanguage switches the language, saves it and updates <html lang dir>', async () => {
    const { i18n, setLanguage, getLanguage } = await loadI18n()

    await setLanguage('ar')

    expect(getLanguage()).toBe('ar')
    expect(i18n.t('nav.dashboard')).toBe('لوحة التحكم')
    expect(localStorage.getItem('crm.language')).toBe('ar')
    expect(document.documentElement).toHaveAttribute('dir', 'rtl')

    await setLanguage('en')

    expect(localStorage.getItem('crm.language')).toBe('en')
    expect(document.documentElement).toHaveAttribute('lang', 'en')
    expect(document.documentElement).toHaveAttribute('dir', 'ltr')
  })
})
```

**Create file: `client/src/i18n/translations.test.ts`** — AC 2: both files complete and consistent.

```ts
import { describe, expect, it } from 'vitest'
import ar from './ar.json'
import en from './en.json'

/** "a.b.c" → value, for every leaf string of a translation file. */
function flatten(tree: object, prefix = ''): Record<string, string> {
  return Object.entries(tree).reduce<Record<string, string>>((all, [key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key
    return typeof value === 'string' ? { ...all, [path]: value } : { ...all, ...flatten(value as object, path) }
  }, {})
}

const english = flatten(en)
const arabic = flatten(ar)

/** Keys whose Arabic value is intentionally not Arabic: the switch shows the *other* language's name. */
const NOT_ARABIC_IN_AR = ['language.switch']

const placeholders = (text: string) => (text.match(/\{\{\s*\w+\s*\}\}/g) ?? []).sort()

describe('translation files', () => {
  it('ar.json and en.json have exactly the same keys', () => {
    expect(Object.keys(arabic).sort()).toEqual(Object.keys(english).sort())
  })

  it.each(Object.keys(english))('"%s" is not empty in either language', (key) => {
    expect(english[key].trim()).not.toBe('')
    expect(arabic[key]?.trim()).not.toBe('')
  })

  it.each(Object.keys(english).filter((key) => !NOT_ARABIC_IN_AR.includes(key)))(
    '"%s" is Arabic text in ar.json',
    (key) => {
      expect(arabic[key]).toMatch(/[؀-ۿ]/)
    },
  )

  it.each(Object.keys(english))('"%s" uses the same {{placeholders}} in both languages', (key) => {
    expect(placeholders(arabic[key] ?? '')).toEqual(placeholders(english[key]))
  })
})
```

**Create file: `client/src/no-hardcoded-text.test.ts`** — AC 2 guard (the CLAUDE.md rule as a test; see "How later stories build on this").

```ts
import { describe, expect, it } from 'vitest'
import en from './i18n/en.json'

// Every app source file as text (tests, test helpers, translation files and generated shadcn components excluded).
const sources = import.meta.glob<string>(
  ['./**/*.{ts,tsx}', '!./**/*.test.{ts,tsx}', '!./test/**', '!./i18n/**', '!./components/ui/**'],
  { query: '?raw', import: 'default', eager: true },
)
const components = Object.entries(sources).filter(([path]) => path.endsWith('.tsx'))

const LETTER = '[A-Za-z\\u0600-\\u06FF]'
/** Text between a closing `>` and the next `<` that contains a letter and no code characters. */
const JSX_TEXT = new RegExp(`>([^<>{}()=;]*${LETTER}[^<>{}()=;]*)<`, 'g')
/** Literal (not `{t(...)}`) values of attributes users see or hear. */
const TEXT_ATTRIBUTE = new RegExp(`\\b(?:aria-label|aria-description|title|placeholder|alt)="[^"]*${LETTER}`, 'g')

/** User-facing text written directly in a component instead of coming from t(). */
function findHardCodedText(source: string): string[] {
  const jsxText = [...source.matchAll(JSX_TEXT)].map((match) => match[1].trim())
  const attributes = [...source.matchAll(TEXT_ATTRIBUTE)].map((match) => match[0])
  return [...jsxText, ...attributes]
}

/** Every English UI string, e.g. "Sign in" (values with {{placeholders}} are skipped). */
function englishValues(tree: object): string[] {
  return Object.values(tree).flatMap((value) =>
    typeof value === 'string' ? (value.includes('{{') ? [] : [value]) : englishValues(value as object),
  )
}

describe('no hard-coded user-facing text (CLAUDE.md: all strings in i18n files)', () => {
  it('detects hard-coded text (self-check of the guard)', () => {
    expect(findHardCodedText('<p>Hello</p>')).toEqual(['Hello'])
    expect(findHardCodedText('<p>\n  مرحبا\n</p>')).toEqual(['مرحبا'])
    expect(findHardCodedText('<Button aria-label="Close">')).toEqual(['aria-label="Close'])
    expect(findHardCodedText('<p>{t(\'shell.comingSoon\')}</p>')).toEqual([])
    expect(findHardCodedText('<Icon aria-hidden="true" className="size-4" />')).toEqual([])
    expect(findHardCodedText('const [x] = useState<string | null>(null)')).toEqual([])
  })

  it('scans the app components', () => {
    expect(components.map(([path]) => path)).toContain('./components/layout/AppHeader.tsx')
  })

  it('has no text written directly in JSX or in aria-label/title/placeholder/alt', () => {
    const offenders = components
      .map(([path, source]) => [path, findHardCodedText(source)] as const)
      .filter(([, found]) => found.length > 0)
    expect(offenders).toEqual([])
  })

  it('keeps every English UI string only in i18n/en.json (no string literal copies in the code)', () => {
    const offenders = englishValues(en)
      .filter((value) => value.length > 3)
      .flatMap((value) =>
        Object.entries(sources)
          .filter(([, source]) => [`'${value}'`, `"${value}"`, `\`${value}\``].some((quoted) => source.includes(quoted)))
          .map(([path]) => `${path}: ${value}`),
      )
    expect(offenders).toEqual([])
  })
})
```

Note: the guard also scans **comments**. Do not quote UI strings (e.g. a translated label in quotes) in code comments — refer to the key instead (`language.switch`).

**Create file: `client/src/api/client.language.test.ts`** — AC 4 (client half).

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { setLanguage } from '../i18n/i18n'
import { apiGet, apiPost } from './client'

function okJson(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
}

function sentHeaders(fetchMock: ReturnType<typeof vi.fn>, call = 0): Record<string, string> {
  return (fetchMock.mock.calls[call][1] as RequestInit).headers as Record<string, string>
}

describe('API client language', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends Accept-Language "en" by default', async () => {
    const fetchMock = vi.fn(async (_path: string, _init?: RequestInit) => okJson({}))
    vi.stubGlobal('fetch', fetchMock)

    await apiGet('/api/health')

    expect(sentHeaders(fetchMock)['Accept-Language']).toBe('en')
  })

  it('sends Accept-Language of the language the user switched to', async () => {
    const fetchMock = vi.fn(async (_path: string, _init?: RequestInit) => okJson({}))
    vi.stubGlobal('fetch', fetchMock)

    await setLanguage('ar')
    await apiPost('/api/auth/login', { email: '', password: '' })
    await setLanguage('en')
    await apiGet('/api/health')

    expect(sentHeaders(fetchMock, 0)['Accept-Language']).toBe('ar')
    expect(sentHeaders(fetchMock, 1)['Accept-Language']).toBe('en')
  })
})
```

(`vi.fn(async () => okJson({}))` creates a **new** `Response` per call; `mockResolvedValue(okJson({}))` would reuse one body and fail with "Body is unusable".)

**File: `client/src/theme.test.ts`** — add this test right before `it('scans the app sources', …)` (line 54):

```ts
  it('loads a font with Arabic glyphs after the Latin font (Geist has no Arabic)', () => {
    expect(css).toContain('@import "@fontsource-variable/noto-sans-arabic";')
    expect(block('@theme inline')).toContain(`--font-sans: 'Geist Variable', 'Noto Sans Arabic Variable', sans-serif;`)
  })
```

Run `npm test` → **Red**: 6 test files fail (`App.i18n.test.tsx` 7 tests: no switch button; `theme.test.ts` font test; `no-hardcoded-text.test.ts`, `translations.test.ts`, `i18n/i18n.test.ts`, `client.language.test.ts` cannot load — `i18n/en.json` / `i18n/i18n.ts` do not exist). The 84 existing tests still pass.

### 2 — Packages, direction component, translation files

```bash
npm install i18next@26.4.2 react-i18next@17.0.15 @fontsource-variable/noto-sans-arabic@5.3.0
npx shadcn@4.21.2 add direction -y
```

- `add direction` creates `src/components/ui/direction.tsx` only. **Do not edit** it (or any file in `src/components/ui/`).
- `package.json` `dependencies` gain exactly `"@fontsource-variable/noto-sans-arabic": "^5.3.0"`, `"i18next": "^26.4.2"`, `"react-i18next": "^17.0.15"`.

**Create file: `client/src/i18n/en.json`**

```json
{
  "app": {
    "name": "Customer Support CRM"
  },
  "language": {
    "switch": "العربية"
  },
  "nav": {
    "dashboard": "Dashboard",
    "tickets": "Tickets",
    "customers": "Customers",
    "knowledgeBase": "Knowledge base",
    "reports": "Reports",
    "users": "Users"
  },
  "shell": {
    "mainNavigation": "Main navigation",
    "toggleSidebar": "Show or hide the sidebar",
    "comingSoon": "This area is coming soon."
  },
  "dashboard": {
    "welcome": "Welcome, {{name}}",
    "apiStatus": "API status",
    "apiOk": "ok",
    "apiLoading": "loading",
    "apiUnavailable": "unavailable"
  },
  "auth": {
    "signInTitle": "Sign in",
    "signInDescription": "Sign in with your work email and password.",
    "email": "Email",
    "password": "Password",
    "signIn": "Sign in",
    "signingIn": "Signing in…",
    "emailRequired": "Enter your email.",
    "emailInvalid": "Enter a valid email address.",
    "passwordRequired": "Enter your password.",
    "invalidCredentials": "Invalid email or password.",
    "signOut": "Sign out"
  },
  "errors": {
    "network": "Cannot reach the server. Check your connection and try again.",
    "badRequest": "The request is invalid. Check the entered data.",
    "forbidden": "You do not have permission to do this.",
    "notFound": "The requested item was not found.",
    "conflict": "This change conflicts with existing data.",
    "generic": "Something went wrong. Please try again.",
    "reference": "Reference: {{id}}"
  },
  "toast": {
    "notifications": "Notifications",
    "close": "Close notification"
  }
}
```

(Every English string is identical to the CRM-3 / CRM-5 / CRM-2 modules, so all existing English tests stay valid.)

**Create file: `client/src/i18n/ar.json`**

```json
{
  "app": {
    "name": "نظام إدارة دعم العملاء"
  },
  "language": {
    "switch": "English"
  },
  "nav": {
    "dashboard": "لوحة التحكم",
    "tickets": "التذاكر",
    "customers": "العملاء",
    "knowledgeBase": "قاعدة المعرفة",
    "reports": "التقارير",
    "users": "المستخدمون"
  },
  "shell": {
    "mainNavigation": "التنقل الرئيسي",
    "toggleSidebar": "إظهار القائمة الجانبية أو إخفاؤها",
    "comingSoon": "هذا القسم قادم قريباً."
  },
  "dashboard": {
    "welcome": "مرحباً، {{name}}",
    "apiStatus": "حالة الخادم",
    "apiOk": "يعمل",
    "apiLoading": "جارٍ التحميل",
    "apiUnavailable": "غير متاح"
  },
  "auth": {
    "signInTitle": "تسجيل الدخول",
    "signInDescription": "سجّل الدخول ببريد العمل الإلكتروني وكلمة المرور.",
    "email": "البريد الإلكتروني",
    "password": "كلمة المرور",
    "signIn": "تسجيل الدخول",
    "signingIn": "جارٍ تسجيل الدخول…",
    "emailRequired": "أدخل بريدك الإلكتروني.",
    "emailInvalid": "أدخل بريداً إلكترونياً صحيحاً.",
    "passwordRequired": "أدخل كلمة المرور.",
    "invalidCredentials": "البريد الإلكتروني أو كلمة المرور غير صحيحة.",
    "signOut": "تسجيل الخروج"
  },
  "errors": {
    "network": "تعذّر الاتصال بالخادم. تحقق من اتصالك وحاول مرة أخرى.",
    "badRequest": "الطلب غير صالح. تحقق من البيانات المدخلة.",
    "forbidden": "ليست لديك صلاحية لتنفيذ هذا الإجراء.",
    "notFound": "العنصر المطلوب غير موجود.",
    "conflict": "يتعارض هذا التغيير مع بيانات موجودة.",
    "generic": "حدث خطأ ما. حاول مرة أخرى.",
    "reference": "المرجع: {{id}}"
  },
  "toast": {
    "notifications": "الإشعارات",
    "close": "إغلاق الإشعار"
  }
}
```

Save both files as **UTF-8**. `language.switch` is the *other* language's own name (that is why it is "العربية" in `en.json`).

Run `npm test` → still **Red** for the right reasons: `translations.test.ts` is **green** (108 tests); `no-hardcoded-text.test.ts` fails "keeps every English UI string only in i18n/en.json" listing every string of `./app/messages.ts`, `./features/auth/auth-messages.ts` and `./api/error-messages.ts`; `App.i18n.test.tsx` (7) and the font test fail; `i18n.test.ts` and `client.language.test.ts` cannot load (`./i18n` missing).

### 3 — i18n instance, typed keys, language switch (Green, part 1)

**Create file: `client/src/i18n/i18n.ts`**

```ts
import i18next from 'i18next'
import { initReactI18next } from 'react-i18next'
import ar from './ar.json'
import en from './en.json'

/** UI languages. The first one is the default. The API supports the same list (Accept-Language). */
export const supportedLanguages = ['en', 'ar'] as const
export type Language = (typeof supportedLanguages)[number]

const DEFAULT_LANGUAGE: Language = 'en'
const STORAGE_KEY = 'crm.language'

function isLanguage(value: unknown): value is Language {
  return supportedLanguages.includes(value as Language)
}

/** The language saved by setLanguage (survives a reload), or English. */
function readStoredLanguage(): Language {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    return isLanguage(stored) ? stored : DEFAULT_LANGUAGE
  } catch {
    return DEFAULT_LANGUAGE
  }
}

/** The app's i18next instance (own instance, so tests can load a fresh one with vi.resetModules()). */
export const i18n = i18next.createInstance()

// <html lang dir> always follows the current language, including the initial one (CSS, fonts, shadcn RTL variants).
i18n.on('languageChanged', (language) => {
  document.documentElement.lang = language
  document.documentElement.dir = i18n.dir(language)
})

void i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, ar: { translation: ar } },
  lng: readStoredLanguage(),
  fallbackLng: DEFAULT_LANGUAGE,
  supportedLngs: supportedLanguages,
  // Translations are bundled: initialise synchronously, so the first render already has the right language.
  initAsync: false,
  // React escapes rendered text already.
  interpolation: { escapeValue: false },
})

/** The current UI language. */
export function getLanguage(): Language {
  return isLanguage(i18n.resolvedLanguage) ? i18n.resolvedLanguage : DEFAULT_LANGUAGE
}

/** Switches the UI language and remembers the choice for the next visit. */
export async function setLanguage(language: Language): Promise<void> {
  try {
    localStorage.setItem(STORAGE_KEY, language)
  } catch {
    // Storage blocked (private mode): the switch still works for this visit.
  }
  await i18n.changeLanguage(language)
}
```

**Create file: `client/src/i18n/i18next.d.ts`**

```ts
import 'i18next'
import type en from './en.json'

// Typed translation keys: t('auth.signIn') compiles, t('auth.typo') fails `npm run build` (tsc -b).
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'translation'
    resources: {
      translation: typeof en
    }
  }
}
```

**File: `client/src/main.tsx`** — add the side-effect import after line 3 (`import './index.css'`), so i18n is initialised (and `<html lang dir>` set) before the first render:

```tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './i18n/i18n'
import App from './App.tsx'
```

(lines 6–10 unchanged.)

**File: `client/src/test/setup.ts`** — final content (adds the import and an English reset; `afterEach` becomes `async`):

```ts
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import { i18n } from '../i18n/i18n'

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

afterEach(async () => {
  cleanup()
  localStorage.clear()
  // BrowserRouter reads the real URL: start every test at "/".
  window.history.replaceState(null, '', '/')
  // Every test starts in English (also resets <html lang="en" dir="ltr">).
  await i18n.changeLanguage('en')
})
```

**Create file: `client/src/components/LanguageSwitcher.tsx`** (used by the login page and the header, so it lives in `components/`, not `components/layout/`)

```tsx
import { LanguagesIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { setLanguage } from '@/i18n/i18n'

/** Switches between Arabic and English. The label (language.switch) is the other language's own name. */
export function LanguageSwitcher() {
  const { t, i18n } = useTranslation()
  const otherLanguage = i18n.resolvedLanguage === 'ar' ? 'en' : 'ar'

  return (
    <Button variant="ghost" size="sm" lang={otherLanguage} onClick={() => void setLanguage(otherLanguage)}>
      <LanguagesIcon aria-hidden="true" />
      {t('language.switch')}
    </Button>
  )
}
```

(`lang` on the button lets screen readers pronounce "العربية" / "English" correctly.)

**File: `client/src/App.tsx`** — replace the whole file (adds `DirectionProvider` around everything; `App` re-renders on language change through `useTranslation`):

```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BrowserRouter } from 'react-router'
import { AppRoutes } from '@/app/AppRoutes'
import { createQueryClient } from '@/app/query-client'
import { getAccessToken, subscribeToSession } from '@/auth/session'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { DirectionProvider } from '@/components/ui/direction'

function App() {
  const [queryClient] = useState(createQueryClient)
  // Re-renders on every language change; Radix primitives (menus, sheets, …) read the direction from here.
  const { i18n } = useTranslation()

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
    <DirectionProvider dir={i18n.dir()}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
        <ApiErrorToaster />
      </QueryClientProvider>
    </DirectionProvider>
  )
}

export default App
```

**File: `client/index.html`** — line 2: `<html lang="en" dir="ltr">` (the state before the script runs; `i18n.ts` overwrites both at start-up).

**File: `client/src/index.css`** — two lines only (the rest of the generated file stays untouched):

- after line 4 `@import "@fontsource-variable/geist";` add `@import "@fontsource-variable/noto-sans-arabic";`
- line 10 becomes `    --font-sans: 'Geist Variable', 'Noto Sans Arabic Variable', sans-serif;`

Latin glyphs come from Geist, Arabic glyphs fall through to Noto Sans Arabic — no per-language font switching needed.

### 4 — Move every string to i18n (Green, part 2)

**Delete file: `client/src/app/messages.ts`** and **delete file: `client/src/features/auth/auth-messages.ts`** (`git rm`).

**File: `client/src/app/navigation.ts`** — replace the whole file (`NavigationId` moves here, derived from `en.json`):

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
import type en from '@/i18n/en.json'

/** Key of a sidebar area: its label is the translation `nav.<id>` in src/i18n/{en,ar}.json. */
export type NavigationId = keyof (typeof en)['nav']

export interface NavigationItem {
  /** Translation key suffix: the label is t(`nav.${id}`). */
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

**File: `client/src/api/error-messages.ts`** — replace the whole file (same exported functions, so `ApiErrorToaster` and its tests need no change):

```ts
import { i18n } from '../i18n/i18n'
import type { ApiError } from './errors'

// Toast text for a failed API call, in the current UI language (keys under "errors" in src/i18n/{en,ar}.json).
// The text is chosen by status, not by the server title, so every toast is translated on the client.
export function getApiErrorMessage(error: ApiError): string {
  switch (error.status) {
    case 0:
      return i18n.t('errors.network')
    case 400:
      return i18n.t('errors.badRequest')
    case 403:
      return i18n.t('errors.forbidden')
    case 404:
      return i18n.t('errors.notFound')
    case 409:
      return i18n.t('errors.conflict')
    default:
      return i18n.t('errors.generic')
  }
}

export function getApiErrorDescription(error: ApiError): string | undefined {
  return error.correlationId ? i18n.t('errors.reference', { id: error.correlationId }) : undefined
}
```

**File: `client/src/api/client.ts`** — two edits:

- after line 1 add `import { getLanguage } from '../i18n/i18n'`
- replace line 40 with:

```ts
  // Accept-Language: the API answers validation messages and ProblemDetails in the UI language (ar / en).
  const headers: Record<string, string> = { Accept: 'application/json', 'Accept-Language': getLanguage() }
```

**File: `client/src/components/ApiErrorToaster.tsx`** — replace the whole file (listener unchanged; the `Toaster` gets direction and translated a11y labels — CRM-5 contract):

```tsx
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Toaster } from '@/components/ui/sonner'
import { onApiError } from '../api/client'
import { getApiErrorDescription, getApiErrorMessage } from '../api/error-messages'

/**
 * Mount once at the app root: renders the toast container and shows an error toast
 * for every failed API call, in the current UI language and direction.
 */
export function ApiErrorToaster() {
  const { t, i18n } = useTranslation()

  useEffect(
    () =>
      onApiError((error) => {
        // 401 is not a toast: the login form shows wrong credentials inline, and an expired
        // session clears the token so the app shows the sign-in form again (CRM-2).
        if (error.status === 401) return
        toast.error(getApiErrorMessage(error), { description: getApiErrorDescription(error) })
      }),
    [],
  )

  return (
    <Toaster
      position="top-center"
      closeButton
      dir={i18n.dir()}
      containerAriaLabel={t('toast.notifications')}
      // Replaces the wrapper's toastOptions, so its "cn-toast" class is repeated here.
      toastOptions={{ classNames: { toast: 'cn-toast' }, closeButtonAriaLabel: t('toast.close') }}
    />
  )
}
```

**File: `client/src/features/auth/login-schema.ts`** — replace the whole file (factory instead of a module-level schema):

```ts
import type { TFunction } from 'i18next'
import { z } from 'zod'

/**
 * Client-side checks before calling POST /api/auth/login (the server validates again).
 * Built with the current `t`, so the messages are in the current UI language.
 */
export function createLoginSchema(t: TFunction) {
  return z.object({
    email: z.string().trim().min(1, t('auth.emailRequired')).pipe(z.email(t('auth.emailInvalid'))),
    password: z.string().min(1, t('auth.passwordRequired')),
  })
}

export type LoginValues = z.infer<ReturnType<typeof createLoginSchema>>
```

**File: `client/src/features/auth/LoginForm.tsx`** — replace the whole file (wrong credentials become a boolean, so the message re-renders in the new language):

```tsx
import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { isApiError } from '@/api/errors'
import { signIn } from '@/auth/sign-in'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { createLoginSchema, type LoginValues } from './login-schema'

/** Email + password form (react-hook-form + zod). On success the session changes and LoginPage redirects. */
export function LoginForm() {
  const { t } = useTranslation()
  const [invalidCredentials, setInvalidCredentials] = useState(false)
  // `t` changes with the language, so the next validation uses messages in the new language.
  const schema = useMemo(() => createLoginSchema(t), [t])
  const form = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  async function onSubmit(values: LoginValues) {
    setInvalidCredentials(false)
    try {
      await signIn(values.email, values.password)
    } catch (caught) {
      // Wrong credentials are shown here; every other failure already raised a toast (ApiErrorToaster).
      if (isApiError(caught) && caught.status === 401) setInvalidCredentials(true)
    }
  }

  const isSubmitting = form.formState.isSubmitting

  return (
    <form aria-label={t('auth.signInTitle')} noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <Controller
          name="email"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="login-email">{t('auth.email')}</FieldLabel>
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
              <FieldLabel htmlFor="login-password">{t('auth.password')}</FieldLabel>
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
        {invalidCredentials ? <FieldError>{t('auth.invalidCredentials')}</FieldError> : null}
        <Button type="submit" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? t('auth.signingIn') : t('auth.signIn')}
        </Button>
      </FieldGroup>
    </form>
  )
}
```

**File: `client/src/pages/auth/LoginPage.tsx`** — replace the whole file (adds the switch in the card header via shadcn `CardAction`, which exists in `components/ui/card.tsx` line 58):

```tsx
import { useTranslation } from 'react-i18next'
import { Navigate, useLocation } from 'react-router'
import { getReturnPath } from '@/app/return-path'
import { useIsAuthenticated } from '@/auth/useIsAuthenticated'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { LoginForm } from '@/features/auth/LoginForm'

export function LoginPage() {
  const { t } = useTranslation()
  const isAuthenticated = useIsAuthenticated()
  const location = useLocation()

  // Already signed in, or just signed in: go to the page the user asked for (default: dashboard).
  if (isAuthenticated) return <Navigate to={getReturnPath(location.state)} replace />

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl font-semibold">{t('app.name')}</h1>
          </CardTitle>
          <CardDescription>{t('auth.signInDescription')}</CardDescription>
          <CardAction>
            <LanguageSwitcher />
          </CardAction>
        </CardHeader>
        <CardContent>
          <LoginForm />
        </CardContent>
      </Card>
    </div>
  )
}
```

**File: `client/src/pages/dashboard/DashboardPage.tsx`** — replace the whole file (the raw server value "ok" becomes the translated `dashboard.apiOk`):

```tsx
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getHealth } from '@/api/health'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useCurrentUser } from '@/features/auth/useCurrentUser'

/** Placeholder dashboard: welcome line + API health. Real widgets come with the reports stories. */
export function DashboardPage() {
  const { t } = useTranslation()
  const { data: user } = useCurrentUser()
  const health = useQuery({ queryKey: ['health'], queryFn: ({ signal }) => getHealth(signal) })

  const apiStatus = health.isError
    ? t('dashboard.apiUnavailable')
    : health.isSuccess
      ? t('dashboard.apiOk')
      : t('dashboard.apiLoading')

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.dashboard')}</h1>
        {user ? <p className="text-muted-foreground">{t('dashboard.welcome', { name: user.fullName })}</p> : null}
      </div>
      <Card className="max-w-sm">
        <CardHeader>
          <CardTitle>{t('dashboard.apiStatus')}</CardTitle>
        </CardHeader>
        <CardContent>
          <p>{apiStatus}</p>
        </CardContent>
      </Card>
    </div>
  )
}
```

**File: `client/src/pages/coming-soon/ComingSoonPage.tsx`** — replace the whole file:

```tsx
import { useTranslation } from 'react-i18next'
import type { NavigationId } from '@/app/navigation'

/** Placeholder for a sidebar area whose story is not built yet (keeps every navigation link working). */
export function ComingSoonPage({ area }: { area: NavigationId }) {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-1">
      <h1 className="text-2xl font-semibold">{t(`nav.${area}`)}</h1>
      <p className="text-muted-foreground">{t('shell.comingSoon')}</p>
    </div>
  )
}
```

### 5 — Layout: sidebar side/direction, header switch (Green, part 3)

**File: `client/src/components/layout/AppSidebar.tsx`** — replace the whole file:

```tsx
import { useTranslation } from 'react-i18next'
import { NavLink, useMatch } from 'react-router'
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
  const { t } = useTranslation()
  const isRoot = item.path === '/'
  const isActive = useMatch({ path: item.path, end: isRoot }) !== null
  const { isMobile, setOpenMobile } = useSidebar()
  const Icon = item.icon

  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild isActive={isActive}>
        <NavLink to={item.path} end={isRoot} onClick={() => isMobile && setOpenMobile(false)}>
          <Icon aria-hidden="true" />
          <span>{t(`nav.${item.id}`)}</span>
        </NavLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
  )
}

export function AppSidebar() {
  const { t, i18n } = useTranslation()
  const dir = i18n.dir()

  // The sidebar sits on the reading-start side: left in English, right in Arabic (also the mobile sheet).
  return (
    <Sidebar side={dir === 'rtl' ? 'right' : 'left'} dir={dir}>
      <SidebarHeader>
        <span className="px-2 py-1 text-base font-semibold">{t('app.name')}</span>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <nav aria-label={t('shell.mainNavigation')}>
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

(`side={… 'right' : 'left'}` are prop **values**, not Tailwind classes — the logical-class guard in `theme.test.ts` does not flag them; verified.)

**File: `client/src/components/layout/AppHeader.tsx`** — replace the whole file:

```tsx
import { LogOutIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { signOut } from '@/auth/sign-in'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { useCurrentUser } from '@/features/auth/useCurrentUser'

/** Top bar: sidebar toggle, signed-in user, language switch, sign out. Sign out clears the token; RequireAuth then redirects to /login. */
export function AppHeader() {
  const { t } = useTranslation()
  const { data: user } = useCurrentUser()

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      {/* aria-label replaces the English sr-only text inside the generated shadcn component. */}
      <SidebarTrigger className="-ms-1" aria-label={t('shell.toggleSidebar')} />
      <Separator orientation="vertical" className="me-2 data-[orientation=vertical]:h-4" />
      <div className="ms-auto flex items-center gap-3">
        {user ? <span className="text-sm text-muted-foreground">{user.fullName}</span> : null}
        <LanguageSwitcher />
        <Button variant="outline" size="sm" onClick={signOut}>
          <LogOutIcon aria-hidden="true" />
          {t('auth.signOut')}
        </Button>
      </div>
    </header>
  )
}
```

`client/src/components/layout/AppLayout.tsx`, `client/src/app/AppRoutes.tsx`, `client/src/app/RequireAuth.tsx` are **unchanged**.

Run `npm test` → **Green**: **213 passed** in 16 files (4 `App.auth.test.tsx`, 7 `App.i18n.test.tsx`, 10 `App.layout.test.tsx`, 1 `App.toast.test.tsx`, 4 `api/client.auth.test.ts`, 2 `api/client.language.test.ts`, 3 `api/client.test.ts`, 2 `api/health.test.ts`, 10 `app/return-path.test.ts`, 4 `auth/session.test.ts`, 4 `components/ApiErrorToaster.test.tsx`, 7 `i18n/i18n.test.ts`, 108 `i18n/translations.test.ts`, 4 `no-hardcoded-text.test.ts`, 3 `pages/dashboard/DashboardPage.test.tsx`, 40 `theme.test.ts`). Then `npm run build` (typed keys checked by `tsc -b`) and `npm run lint` (exit 0, no output).

---

## Backend Tasks

All commands run from `server/`. No new NuGet packages (request localization is part of ASP.NET Core; FluentValidation already ships Arabic).

### 6 — Tests first (Red)

**Create file: `server/tests/Crm.UnitTests/Localization/UiCulture.cs`**

```csharp
using System.Globalization;

namespace Crm.UnitTests.Localization;

/// <summary>Runs code with a given UI culture and restores the previous one (tests must not leak culture).</summary>
internal static class UiCulture
{
    public static T Use<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
```

**Create file: `server/tests/Crm.UnitTests/Localization/LocalizedTextTests.cs`**

```csharp
using Crm.Application.Common.Localization;

namespace Crm.UnitTests.Localization;

public class LocalizedTextTests
{
    [Theory]
    [InlineData("ar")]
    [InlineData("ar-SA")]
    public void Get_WithArabicUiCulture_ReturnsArabic(string culture)
    {
        var text = UiCulture.Use(culture, () => LocalizedText.Get("Hello", "مرحبا"));

        Assert.Equal("مرحبا", text);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("fr")]
    public void Get_WithAnyOtherUiCulture_ReturnsEnglish(string culture)
    {
        var text = UiCulture.Use(culture, () => LocalizedText.Get("Hello", "مرحبا"));

        Assert.Equal("Hello", text);
    }
}
```

**Create file: `server/tests/Crm.UnitTests/Localization/LocalizedTextCatalogTests.cs`** — guard for every server text class (now and in later stories):

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using Crm.Application.Auth;
using Crm.Application.Common.Localization;

namespace Crm.UnitTests.Localization;

/// <summary>
/// Guard: every user-facing text class in the Application layer (a static class named "*Text") returns
/// English text for "en" and Arabic text for "ar". Text classes added by later stories are picked up automatically.
/// </summary>
public partial class LocalizedTextCatalogTests
{
    [GeneratedRegex(@"[؀-ۿ]")]
    private static partial Regex ArabicLetter();

    private static readonly Type[] TextClasses = typeof(Crm.Application.AssemblyReference).Assembly.GetTypes()
        .Where(type => type is { IsAbstract: true, IsSealed: true } // static class
                       && type.Name.EndsWith("Text", StringComparison.Ordinal)
                       && type != typeof(LocalizedText))
        .ToArray();

    public static TheoryData<string> TextProperties()
    {
        var data = new TheoryData<string>();
        foreach (var property in TextClasses
                     .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Static))
                     .Where(property => property.PropertyType == typeof(string)))
        {
            data.Add($"{property.DeclaringType!.Name}.{property.Name}");
        }

        return data;
    }

    [Fact]
    public void Catalog_FindsTheTextClasses()
    {
        Assert.Contains(typeof(AuthText), TextClasses);
        Assert.Contains(typeof(ErrorText), TextClasses);
    }

    [Theory]
    [MemberData(nameof(TextProperties))]
    public void TextProperty_HasEnglishAndArabicText(string name)
    {
        var parts = name.Split('.');
        var property = TextClasses.Single(type => type.Name == parts[0])
            .GetProperty(parts[1], BindingFlags.Public | BindingFlags.Static)!;

        var english = UiCulture.Use("en", () => (string)property.GetValue(null)!);
        var arabic = UiCulture.Use("ar", () => (string)property.GetValue(null)!);

        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.DoesNotMatch(ArabicLetter(), english);
        Assert.Matches(ArabicLetter(), arabic);
    }
}
```

**File: `server/tests/Crm.UnitTests/Auth/LoginRequestValidatorTests.cs`** — add `using Crm.UnitTests.Localization;` after line 1 and these two tests after `InvalidRequest_ReportsTheField` (keep the existing tests):

```csharp
    [Fact]
    public void EmptyRequest_InEnglish_HasEnglishMessages()
    {
        var messages = UiCulture.Use("en", () =>
            _validator.Validate(new LoginRequest("", "")).Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("'Email' must not be empty.", messages);
        Assert.Contains("'Password' must not be empty.", messages);
    }

    [Fact]
    public void EmptyRequest_InArabic_HasArabicMessagesAndFieldNames()
    {
        var messages = UiCulture.Use("ar", () =>
            _validator.Validate(new LoginRequest("", "")).Errors.Select(e => e.ErrorMessage).ToList());

        Assert.Contains("'البريد الإلكتروني' لا يجب أن يكون فارغاً.", messages);
        Assert.Contains("'كلمة المرور' لا يجب أن يكون فارغاً.", messages);
    }
```

**Create file: `server/tests/Crm.Api.IntegrationTests/Localization/AcceptLanguageTests.cs`** — AC 4 end to end:

```csharp
using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Localization;

public class AcceptLanguageTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string LoginPath = "/api/auth/login";
    private const string EmailRequiredEnglish = "'Email' must not be empty.";
    private const string EmailRequiredArabic = "'البريد الإلكتروني' لا يجب أن يكون فارغاً.";

    private HttpClient ClientWithLanguage(string? acceptLanguage)
    {
        var client = factory.CreateClient();
        if (acceptLanguage is not null)
        {
            client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        }

        return client;
    }

    private static async Task<HttpValidationProblemDetails> PostEmptyLoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(LoginPath, new { email = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>())!;
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("ar-SA")]
    [InlineData("ar-EG,ar;q=0.9,en;q=0.8")]
    public async Task ValidationErrors_WithArabicAcceptLanguage_AreInArabic(string acceptLanguage)
    {
        var problem = await PostEmptyLoginAsync(ClientWithLanguage(acceptLanguage));

        Assert.Contains(EmailRequiredArabic, problem.Errors["email"]);
        Assert.Equal("حدث خطأ واحد أو أكثر في التحقق من البيانات.", problem.Title);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US,en;q=0.9")]
    [InlineData("fr-FR")]
    [InlineData(null)]
    public async Task ValidationErrors_WithEnglishMissingOrUnsupportedAcceptLanguage_AreInEnglish(string? acceptLanguage)
    {
        var problem = await PostEmptyLoginAsync(ClientWithLanguage(acceptLanguage));

        Assert.Contains(EmailRequiredEnglish, problem.Errors["email"]);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
    }

    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("ar-SA", "ar")]
    [InlineData("fr-FR", "en")]
    public async Task Response_HasContentLanguageOfTheChosenLanguage(string acceptLanguage, string expected)
    {
        var response = await ClientWithLanguage(acceptLanguage).GetAsync("/api/health");

        Assert.Equal([expected], response.Content.Headers.ContentLanguage);
    }

    [Fact]
    public async Task WrongPassword_WithArabicAcceptLanguage_ReturnsArabicDetail()
    {
        var response = await ClientWithLanguage("ar").PostAsJsonAsync(LoginPath,
            new { email = CrmApiFactory.SuperAdminEmail, password = "Wrong#Password1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("البريد الإلكتروني أو كلمة المرور غير صحيحة.", problem!.Detail);
    }

    [Theory]
    [InlineData("/_test/errors/not-found", "العنصر المطلوب غير موجود.")]
    [InlineData("/api/does-not-exist", "العنصر المطلوب غير موجود.")]
    [InlineData("/api/auth/me", "يجب تسجيل الدخول.")]
    public async Task ProblemTitle_WithArabicAcceptLanguage_IsInArabic(string path, string expectedTitle)
    {
        var response = await ClientWithLanguage("ar").GetAsync(path);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(expectedTitle, problem!.Title);
    }
}
```

Run `dotnet test` → **Red**: `Crm.UnitTests` does not compile (`CS0234: The type or namespace name 'Localization' does not exist in the namespace 'Crm.Application.Common'`); `Crm.Api.IntegrationTests` **10 failed, 32 passed** (all Arabic / Content-Language / Arabic-title cases fail; the 4 English cases already pass — they guard "English stays the default").

### 7 — Localized text in the Application layer (Green, part 1)

**Create file: `server/src/Crm.Application/Common/Localization/LocalizedText.cs`**

```csharp
using System.Globalization;

namespace Crm.Application.Common.Localization;

/// <summary>
/// Picks the text for the request language. The API sets <see cref="CultureInfo.CurrentUICulture"/> from the
/// Accept-Language header (supported: "en", "ar"; default "en").
/// </summary>
public static class LocalizedText
{
    public const string English = "en";
    public const string Arabic = "ar";

    /// <summary>Every UI language the API supports. The first one is the default.</summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } = [English, Arabic];

    /// <summary>Arabic text when the current UI culture is Arabic ("ar", "ar-SA", …), otherwise English.</summary>
    public static string Get(string english, string arabic) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Arabic ? arabic : english;
}
```

**Create file: `server/src/Crm.Application/Common/Localization/ErrorText.cs`**

```csharp
namespace Crm.Application.Common.Localization;

/// <summary>ProblemDetails titles in the request language (used by the API's error handling).</summary>
public static class ErrorText
{
    public static string ValidationFailed => LocalizedText.Get(
        "One or more validation errors occurred.",
        "حدث خطأ واحد أو أكثر في التحقق من البيانات.");

    public static string MalformedRequest => LocalizedText.Get(
        "The request is malformed.",
        "صيغة الطلب غير صحيحة.");

    public static string AuthenticationFailed => LocalizedText.Get(
        "Authentication failed.",
        "فشل التحقق من الهوية.");

    public static string AuthenticationRequired => LocalizedText.Get(
        "Authentication is required.",
        "يجب تسجيل الدخول.");

    public static string Forbidden => LocalizedText.Get(
        "You do not have permission to perform this action.",
        "ليست لديك صلاحية لتنفيذ هذا الإجراء.");

    public static string NotFound => LocalizedText.Get(
        "The requested resource was not found.",
        "العنصر المطلوب غير موجود.");

    public static string Conflict => LocalizedText.Get(
        "The request conflicts with the current state.",
        "يتعارض الطلب مع البيانات الحالية.");

    public static string Unexpected => LocalizedText.Get(
        "An unexpected error occurred.",
        "حدث خطأ غير متوقع.");
}
```

(The English titles are exactly the ones in `GlobalExceptionHandler.cs` lines 56–90 today.)

**Create file: `server/src/Crm.Application/Auth/AuthText.cs`**

```csharp
using Crm.Application.Common.Localization;

namespace Crm.Application.Auth;

/// <summary>User-facing text of the auth feature, in the request language.</summary>
public static class AuthText
{
    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string PasswordField => LocalizedText.Get("Password", "كلمة المرور");

    // One message for unknown email, wrong password and locked-out user: never reveal which one it was.
    public static string InvalidCredentials => LocalizedText.Get(
        "Invalid email or password.",
        "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
}
```

**File: `server/src/Crm.Application/Auth/LoginRequestValidator.cs`** — final content:

```csharp
using FluentValidation;

namespace Crm.Application.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // FluentValidation translates its built-in messages (CurrentUICulture); WithName translates the field name.
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256).WithName(_ => AuthText.EmailField);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128).WithName(_ => AuthText.PasswordField);
    }
}
```

(`WithName` sets the display name only; `PropertyName` stays `Email` / `Password`, so the camelCase error keys `email` / `password` and `InvalidRequest_ReportsTheField` are unchanged.)

**File: `server/src/Crm.Infrastructure/Identity/AuthService.cs`** — delete lines 14–15 (the comment and `public const string InvalidCredentialsMessage = …;` — the comment moved to `AuthText`) and replace `InvalidCredentialsMessage` on lines 24 and 30 with `AuthText.InvalidCredentials` (`using Crm.Application.Auth;` is already on line 1).

Run `dotnet test` → unit tests compile and pass (**31**); integration still 10 failed (nothing reads the request language yet).

### 8 — Request localization in the API (Green, part 2)

**Create file: `server/src/Crm.Api/Localization/LocalizationExtensions.cs`**

```csharp
using System.Globalization;
using Crm.Application.Common.Localization;
using Microsoft.AspNetCore.Localization;

namespace Crm.Api.Localization;

public static class LocalizationExtensions
{
    /// <summary>
    /// UI language per request from the Accept-Language header: "ar" (also "ar-SA", "ar-EG", …) or "en";
    /// anything else → "en". Only the UI culture changes (messages); the formatting culture stays "en",
    /// so dates and numbers are never formatted with the Arabic (Hijri) calendar by accident.
    /// </summary>
    public static IServiceCollection AddCrmLocalization(this IServiceCollection services)
    {
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(LocalizedText.English);
            options.SupportedCultures = [new CultureInfo(LocalizedText.English)];
            options.SupportedUICultures = [.. LocalizedText.SupportedLanguages.Select(language => new CultureInfo(language))];
            options.FallBackToParentUICultures = true;
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
            options.ApplyCurrentCultureToResponseHeaders = true;
        });
        return services;
    }

    /// <summary>Must run before UseCrmErrorHandling, so ProblemDetails are written in the request language.</summary>
    public static WebApplication UseCrmLocalization(this WebApplication app)
    {
        app.UseRequestLocalization();
        return app;
    }
}
```

(`RequestCultureProviders` is restricted to the header: no `?culture=` query string or cookie — the client is the single source of the language.)

**File: `server/src/Crm.Api/Program.cs`** — three edits (final lines 1–23):

```csharp
using Crm.Api.Auth;
using Crm.Api.Endpoints;
using Crm.Api.ErrorHandling;
using Crm.Api.Localization;
using Crm.Application;
using Crm.Infrastructure;
using Crm.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCrmLocalization();
builder.Services.AddCrmErrorHandling();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddCrmAuthentication(builder.Configuration);

var app = builder.Build();

// Localization first: the error handler and status-code pages write ProblemDetails in the request language.
app.UseCrmLocalization();
app.UseCrmErrorHandling();
app.UseAuthentication();
```

Everything after `app.UseAuthentication();` stays as it is. **Do not** put `UseCrmLocalization()` after `UseCrmErrorHandling()`: the titles then stay English (probed — 6 tests fail). This supersedes the CRM-2 note "`UseCrmErrorHandling` must stay the first middleware": it stays first **after** localization, which cannot throw.

**File: `server/src/Crm.Api/ErrorHandling/GlobalExceptionHandler.cs`** — add `using Crm.Application.Common.Localization;` after line 1 and replace the seven title strings in `ToProblemDetails`:

| Line (today) | Old | New |
|---|---|---|
| 56 | `Title = "One or more validation errors occurred.",` | `Title = ErrorText.ValidationFailed,` |
| 61 | `Title = "The request is malformed.",` | `Title = ErrorText.MalformedRequest,` |
| 66 | `Title = "Authentication failed.",` | `Title = ErrorText.AuthenticationFailed,` |
| 72 | `Title = "The requested resource was not found.",` | `Title = ErrorText.NotFound,` |
| 78 | `Title = "The request conflicts with the current state.",` | `Title = ErrorText.Conflict,` |
| 84 | `Title = "You do not have permission to perform this action.",` | `Title = ErrorText.Forbidden,` |
| 90 | `Title = "An unexpected error occurred.",` | `Title = ErrorText.Unexpected,` |

**File: `server/src/Crm.Api/ErrorHandling/ErrorHandlingExtensions.cs`** — final content (adds a title in the request language for responses written by `UseStatusCodePages`, which have no exception):

```csharp
using Crm.Application.Common.Localization;

namespace Crm.Api.ErrorHandling;

public static class ErrorHandlingExtensions
{
    /// <summary>ProblemDetails for every error response, with <c>correlationId</c> and <c>instance</c> filled in.</summary>
    public static IServiceCollection AddCrmErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["correlationId"] =
                    CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);

                // Empty error responses turned into ProblemDetails by UseStatusCodePages (JWT challenge 401,
                // unknown route 404, malformed body 400, …) have no exception: give them a title in the
                // request language. GlobalExceptionHandler sets its own titles.
                if (context.Exception is null)
                {
                    context.ProblemDetails.Title = context.ProblemDetails.Status switch
                    {
                        StatusCodes.Status400BadRequest => ErrorText.MalformedRequest,
                        StatusCodes.Status401Unauthorized => ErrorText.AuthenticationRequired,
                        StatusCodes.Status403Forbidden => ErrorText.Forbidden,
                        StatusCodes.Status404NotFound => ErrorText.NotFound,
                        _ => context.ProblemDetails.Title,
                    };
                }
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>Order matters: correlation id first (outermost), then the exception handler, then status-code pages.</summary>
    public static WebApplication UseCrmErrorHandling(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
```

Run `dotnet build` → 0 warnings, 0 errors. Run `dotnet test` → **Green**: **73 passed** (31 unit: 12 existing + 2 validator + 5 `LocalizedTextTests` + 12 `LocalizedTextCatalogTests` (1 + 11 text properties); 42 integration: 28 existing + 14 `AcceptLanguageTests`).

### 9 — How later stories build on this (write nothing here; for later planners)

- **Every new UI string (all later frontend stories):** add the key to **both** `client/src/i18n/en.json` and `client/src/i18n/ar.json` (group by feature: `customers.*`, `tickets.*`, …), use it with `const { t } = useTranslation()` → `t('customers.title')` in components, or `i18n.t(...)` from `@/i18n/i18n` in plain `.ts` modules evaluated at call time (never at module load — the language can change). `translations.test.ts` fails when a key is missing, empty, not Arabic in `ar.json` or has different `{{placeholders}}`; `no-hardcoded-text.test.ts` fails on JSX text / literal `aria-label`/`title`/`placeholder`/`alt` or on English strings copied into code; `tsc -b` fails on unknown keys. Tests keep querying by the **English** text (the default; `setup.ts` resets to English after each test).
- **zod schemas with messages:** build them in a factory taking `t` (`createLoginSchema(t)` pattern) and memoize with `useMemo(() => create…Schema(t), [t])`.
- **Server-side field errors shown inline (later forms):** the server already answers in the UI language because `client/src/api/client.ts` sends `Accept-Language`; show `error.problem?.errors?.[field]` as-is — do not translate on the client.
- **Every new user-facing server text (later backend stories):** add a property to a static `<Feature>Text` class in `Crm.Application/<Feature>/` (e.g. `CustomerText.NotFound => LocalizedText.Get("…", "…")`) and use it for exception messages (`new NotFoundException(CustomerText.NotFound)`) and for `.WithName(_ => CustomerText.NameField)` in validators. `LocalizedTextCatalogTests` picks up every `*Text` class automatically and fails on a missing / non-Arabic translation. FluentValidation's built-in rule messages are already translated; a custom `.WithMessage(...)` must use `_ => SomeText.Property` (lazy), never a plain string.
- **Integration tests of localized endpoints:** add `Accept-Language` to the client (`client.DefaultRequestHeaders.Add("Accept-Language", "ar")`, see `AcceptLanguageTests.ClientWithLanguage`); without the header the API answers in English, so existing English assertions stay valid.
- **New Radix-based shadcn components** (dropdown-menu, select, popover, …): `npx shadcn@4.21.2 add <name> -y`; they read the direction from `DirectionProvider` in `App.tsx` — no extra wiring.
- **Per-user language (later, optional):** `setLanguage()` is the single entry point; a profile setting would call it after login.
- **Dates/numbers (tickets, SLA stories):** format on the client with `Intl.DateTimeFormat(getLanguage(), …)` / `Intl.NumberFormat`; the server keeps sending UTC ISO dates (formatting culture stays `en`).

---

## Edge Cases & Failure Modes

- **Nothing stored / unsupported value (`"fr"`, `""`, `"AR"`, JSON) in `crm.language`** → English (`readStoredLanguage` in `client/src/i18n/i18n.ts`). Covered by `i18n.test.ts` (`it.each`).
- **`localStorage` blocked (private mode, disabled storage)** → reading falls back to English, writing is skipped in `setLanguage`'s `try/catch`; the switch still works until reload.
- **Browser in Arabic but nothing stored** → English (no browser-language detection in this story; one click switches and is remembered).
- **Language switched while a field error is shown on the login form** → the error stays in the old language until the next validation (submit); the resolver already uses the new `t` (`useMemo` in `LoginForm.tsx`). The wrong-credentials message is a boolean, so it re-renders in the new language immediately.
- **Toast already visible when the language changes** → it keeps the text it was created with (toasts are short-lived); new toasts use the new language (`getApiErrorMessage` reads `i18n.t` at failure time). The `Toaster` direction updates immediately.
- **Text of the mobile sidebar sheet (sr-only "Sidebar" / "Displays the mobile sidebar.", sheet "Close")** → hard-coded inside generated `components/ui/sidebar.tsx` lines 195–198 and `components/ui/sheet.tsx`; they are screen-reader-only and appear only below 768 px. Accepted for this story because `components/ui/` is never hand-edited (CRM-3 rule) and is excluded from the guard. The visible header toggle **is** translated through `aria-label` (overrides the sr-only "Toggle Sidebar" as accessible name; the sr-only text is still in the DOM, which is why the "no English text" test runs on the login page, not inside the layout).
- **`<html>` before JavaScript runs** → `index.html` says `lang="en" dir="ltr"`; `i18n.ts` corrects both before the first React render (side-effect import in `main.tsx` before `App`). A stored Arabic choice therefore causes no LTR flash after the script loads (only the static HTML before it, which is empty).
- **Typo in a translation key** → `npm run build` fails (`TS2345`, typed keys from `i18next.d.ts`). Tests do not type-check, so run the build.
- **Key in `en.json` missing in `ar.json`** → `translations.test.ts` fails; at runtime i18next would fall back to English (`fallbackLng: 'en'`).
- **Quoted UI string in a code comment** → `no-hardcoded-text.test.ts` reports it (the guard reads raw source). Refer to the key instead.
- **Guard false positives** → the JSX regex ignores text containing `( ) = ; { }` (code). If a legitimate non-text JSX child ever trips it, rewrite the JSX (e.g. wrap in `{}`); do not weaken the regex without updating its self-check test.
- **`Accept-Language` with regions / q-values (`ar-SA`, `ar-EG,ar;q=0.9,en;q=0.8`)** → Arabic (`FallBackToParentUICultures`); **unsupported (`fr-FR`) or missing** → English. Covered by `AcceptLanguageTests`.
- **Middleware order** → localization must be before `UseCrmErrorHandling` (`Program.cs`), otherwise ProblemDetails titles stay English while field messages are Arabic. Covered by `ProblemTitle_WithArabicAcceptLanguage_IsInArabic` and the Arabic validation title assertion.
- **Arabic formatting culture (Hijri calendar)** → impossible: `SupportedCultures = [en]`; only the UI culture follows the request (`LocalizationExtensions.AddCrmLocalization`).
- **Culture leaking between unit tests** → `UiCulture.Use` restores the previous UI culture in `finally`.
- **Server texts in logs** → log messages stay English templates (`GlobalExceptionHandler` lines 24–37 unchanged); only response bodies are localized.
- **Empty email fails two rules** (`NotEmpty` + `EmailAddress`) → two messages under `email`; tests use `Assert.Contains`.
- **Existing English assertions** (`LoginTests` lines 54/67, client tests) → unchanged because English is the default on both sides.
- **Bundle size** → JS 631 kB (Vite warning, not an error); the Arabic font subsets are separate `.woff2` files loaded on demand by `unicode-range`.

---

## Test Plan

1. **Component (app level, new)** — `client/src/App.i18n.test.tsx`: `starts in English with <html dir="ltr" lang="en">` (AC 1), `switching to Arabic sets <html dir="rtl" lang="ar"> and translates the page` (AC 1, 2), `switching back to English sets <html dir="ltr" lang="en">` (AC 1), `shows no English text on the Arabic login page, field errors included` (AC 2), `remembers the chosen language for the next visit` (AC 3), `signed in, Arabic: sidebar on the right, navigation and header in Arabic` (AC 1, 2), `signs in in Arabic and shows API error toasts in Arabic` (AC 2).
2. **Unit (new)** — `client/src/i18n/i18n.test.ts`: `starts in English with <html dir="ltr" lang="en"> when nothing is saved`, `starts in the saved language after a reload` (AC 3), 4 × `ignores an unsupported saved value … and starts in English`, `setLanguage switches the language, saves it and updates <html lang dir>` (AC 1, 3).
3. **Guard (new)** — `client/src/i18n/translations.test.ts`: same keys, 36 × not empty, 35 × Arabic in `ar.json`, 36 × same placeholders (AC 2).
4. **Guard (new)** — `client/src/no-hardcoded-text.test.ts`: self-check, scans the components, no JSX/attribute literals, no English string copies outside `en.json` (AC 2).
5. **Unit (new)** — `client/src/api/client.language.test.ts`: `sends Accept-Language "en" by default`, `sends Accept-Language of the language the user switched to` (AC 4).
6. **Guard (modified)** — `client/src/theme.test.ts`: `loads a font with Arabic glyphs after the Latin font (Geist has no Arabic)`.
7. **Unchanged, must stay green** — `App.layout.test.tsx`, `App.auth.test.tsx`, `App.toast.test.tsx`, `ApiErrorToaster.test.tsx`, `DashboardPage.test.tsx`, `client.test.ts`, `client.auth.test.ts`, `health.test.ts`, `session.test.ts`, `return-path.test.ts`.
8. **Unit (backend, new)** — `server/tests/Crm.UnitTests/Localization/LocalizedTextTests.cs`: `Get_WithArabicUiCulture_ReturnsArabic` (ar, ar-SA), `Get_WithAnyOtherUiCulture_ReturnsEnglish` (en, en-US, fr).
9. **Guard (backend, new)** — `server/tests/Crm.UnitTests/Localization/LocalizedTextCatalogTests.cs`: `Catalog_FindsTheTextClasses`, `TextProperty_HasEnglishAndArabicText` × 11 (8 `ErrorText` + 3 `AuthText`).
10. **Unit (backend, modified)** — `server/tests/Crm.UnitTests/Auth/LoginRequestValidatorTests.cs`: `EmptyRequest_InEnglish_HasEnglishMessages`, `EmptyRequest_InArabic_HasArabicMessagesAndFieldNames` (AC 4).
11. **Integration (backend, new)** — `server/tests/Crm.Api.IntegrationTests/Localization/AcceptLanguageTests.cs`: `ValidationErrors_WithArabicAcceptLanguage_AreInArabic` ×3, `ValidationErrors_WithEnglishMissingOrUnsupportedAcceptLanguage_AreInEnglish` ×4, `Response_HasContentLanguageOfTheChosenLanguage` ×3, `WrongPassword_WithArabicAcceptLanguage_ReturnsArabicDetail`, `ProblemTitle_WithArabicAcceptLanguage_IsInArabic` ×3 (AC 4).
12. **Unchanged, must stay green (backend)** — all CRM-5 / CRM-2 tests, incl. `LayerDependencyTests` (Application still free of ASP.NET Core) and `LoginTests` English detail.
13. **Manual smoke** — Verification step 6.

---

## Verification Steps

1. **Frontend tests:** in `client/` run `npm test` — **213 passed** in 16 files, process exits.
2. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed (the "chunks larger than 500 kB" message is a warning).
3. **Frontend lint:** in `client/` run `npm run lint` — exit code 0, no warnings, no errors.
4. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings.
5. **Backend tests:** in `server/` run `dotnet test` — **73 passed** (31 unit, 42 integration).
6. **Manual smoke** (user-secrets from CRM-2 already set):
   - Terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http`.
   - `curl -i -X POST http://localhost:5080/api/auth/login -H "Content-Type: application/json" -H "Accept-Language: ar" -d "{\"email\":\"\",\"password\":\"\"}"` → `400`, `Content-Language: ar`, Arabic `title` and `errors.email` contains "'البريد الإلكتروني' لا يجب أن يكون فارغاً.". Same call without the header → English.
   - Terminal 2: `cd client && npm run dev`, open `http://localhost:5173/login` → English card with a "العربية" button. Click it → whole card in Arabic, right-to-left, Arabic font; DevTools → `<html lang="ar" dir="rtl">`, Local Storage `crm.language = ar`. Reload → still Arabic.
   - Sign in → sidebar on the **right** with Arabic labels, header with "English" and "تسجيل الخروج"; Network tab: every `/api/*` request has `Accept-Language: ar`. Narrow below 768 px and open the sidebar → the sheet slides in from the right.
   - Click "English" → layout flips to LTR, sidebar on the left; reload → English.
   - Stop the API while in Arabic and reload the dashboard → Arabic toast "تعذّر الاتصال بالخادم…" / "حدث خطأ ما…" and "غير متاح".
7. **Regression:** `git status` shows no changes under `.claude/`, `.mcp.json`, `CLAUDE.md`, and no hand edits in `client/src/components/ui/` (only the new generated `direction.tsx`). `git grep -n "shellMessages\|authMessages" -- client/src` finds nothing. `client/src/api/client.ts` is still the only `fetch` caller (`git grep -n "fetch(" -- client/src ':!*.test.*' ':!client/src/test'`).

---

## Done Criteria

- [ ] Switching to Arabic sets `<html dir="rtl" lang="ar">`; switching to English sets `dir="ltr" lang="en"` (`App.i18n.test.tsx` AC 1 tests green).
- [ ] All UI strings come from `client/src/i18n/{en,ar}.json`; `app/messages.ts` and `features/auth/auth-messages.ts` deleted; `translations.test.ts` and `no-hardcoded-text.test.ts` green; typed keys (`i18next.d.ts`) make `npm run build` fail on unknown keys.
- [ ] The selected language persists after reload (`localStorage["crm.language"]`; `starts in the saved language after a reload` green).
- [ ] API validation messages follow `Accept-Language` (ar / en, default en), incl. ProblemDetails titles and the wrong-credentials detail (`AcceptLanguageTests` green); the client sends `Accept-Language` on every call (`client.language.test.ts` green).
- [ ] Sidebar on the right in Arabic (`side`/`dir`), `DirectionProvider` around the app, toasts with `dir` and translated a11y labels, Noto Sans Arabic loaded.
- [ ] Server texts live in `*Text` classes in `Crm.Application` via `LocalizedText.Get`; `LocalizedTextCatalogTests` and `LayerDependencyTests` green; `UseCrmLocalization()` runs before `UseCrmErrorHandling()`.
- [ ] Packages: `i18next@26.4.2`, `react-i18next@17.0.15`, `@fontsource-variable/noto-sans-arabic@5.3.0`; `components/ui/direction.tsx` added with `shadcn@4.21.2`; no hand edits in `components/ui/`.
- [ ] `npm test` (213), `npm run build`, `npm run lint`, `dotnet build`, `dotnet test` (73) all pass.
- [ ] Committed on `feature/crm-4-i18n-rtl` with message `CRM-4: Arabic / English with RTL support`.
- [ ] Overview `00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 06.**
