# Story 63 — Custom branding (colors & logo) (Story: CRM-63)

## Prerequisites

- System settings (CRM-35): table `SystemSettings` + `ISettingsRepository`; file storage `IFileStorage` (CRM-11); email sending (`SmtpEmailProvider`, `NotificationEmailSender`); client theme in `client/src/index.css` (shadcn CSS variables); portal layout and login page.
- Migration: **none** (branding is stored in `SystemSettings` keys `branding.*`; the logo file in `IFileStorage`). New permission: none (`settings.manage`, SuperAdmin). New audit action `branding.updated`.
- Reference only: CRM 01 `specs/57-custom-branding` (fallback to default branding, 400 on invalid fields).

## Story Goal

1. **AC 1** — `PUT /api/branding` `{ primaryColor, secondaryColor }` and `PUT /api/branding/logo` (multipart `file`) / `DELETE /api/branding/logo`, all `settings.manage` (SuperAdmin; others 403, anonymous 401). Empty / null colour = back to the default theme.
2. **AC 2** — `GET /api/branding` is **anonymous** (login page and portal need it) and returns `{ primaryColor, secondaryColor, logoUrl }` (nulls = defaults). The client fetches it at start and sets `--primary`, `--primary-foreground`, `--secondary`, `--secondary-foreground` (and `--ring`, sidebar primary) on `<html>` — no rebuild. The foreground colour is chosen black/white by contrast.
3. **AC 3** — Colour must be `#RGB` or `#RRGGBB` (else 400 on `primaryColor` / `secondaryColor`). Logo: png, jpg, webp or gif by extension **and** file signature, at most 2 MB (2 097 152 bytes, inclusive): 400 on `file` otherwise; SVG is refused (scripts).
4. **AC 4** — Staff app (sidebar header + login), customer portal header, and emails: notification and channel emails get an HTML part (plain text kept) with a header band in the primary colour and the logo embedded (`cid:`). Without branding nothing changes (plain text only).

## Context — Read These Files First

1. `server/src/Crm.Application/Settings/SettingsContracts.cs` (`ISettingsRepository`), `SystemSettingsService.cs` (how keys are written), `Crm.Domain/Settings/SystemSetting.cs`.
2. `server/src/Crm.Application/Customers/Attachments/AttachmentRules.cs`, `CustomersEndpoints.cs` (multipart upload pattern), `Crm.Application/Common/Files/IFileStorage.cs`.
3. `server/src/Crm.Infrastructure/Channels/Email/SmtpEmailProvider.cs`, `EmailMessageBuilder.cs`, `Notifications/NotificationEmailSender.cs`.
4. `client/src/index.css` (theme variables), `client/src/main.tsx`, `components/layout/AppSidebar.tsx`, `components/portal/PortalLayout.tsx`, `pages/auth/LoginPage.tsx`, `pages/settings/SettingsPage.tsx`.

## Backend Tasks

- `Crm.Application/Branding/`: `BrandingRules` (colour regex, 2 MB, extensions + signatures), contracts, `IBrandingService`/`BrandingService` (get, update colours with audit, upload / remove logo through `IFileStorage` under `branding/logo-{guid}`; the stored key doubles as the cache-busting `?v=`), `BrandingText`, `EmailBranding` record.
- `AuditActions.BrandingUpdated`.
- `Crm.Infrastructure/Channels/Email/BrandedEmail.cs` (MimeKit `BodyBuilder`: text + HTML + linked logo); `SmtpEmailProvider`, `EmailMessageBuilder.Build`, `NotificationEmailSender` take an optional branding.
- `Crm.Api/Endpoints/BrandingEndpoints.cs`: `GET /api/branding` (anonymous), `GET /api/branding/logo` (anonymous, `nosniff`, restrictive CSP, short cache), `PUT /api/branding`, `PUT|DELETE /api/branding/logo` (`settings.manage`).

## Frontend Tasks

- `api/branding.ts`; `features/branding/BrandingProvider.tsx` (fetch + apply CSS variables, `useBranding`), `branding-colors.ts` (contrast helper); logo in `AppSidebar`, `LoginPage`, `PortalLayout` (with the app name as alt text).
- `pages/branding/BrandingPage.tsx` (colour fields with live swatch, logo upload / remove, reset), route `/branding` + nav item (`settings.manage`); i18n en/ar; no hex literals in components.

## Edge Cases & Failure Modes

- No branding stored: defaults (theme untouched, no logo).
- Re-uploading a logo deletes the previous file; a failed upload keeps the old one.
- File named `.png` with other content: 400 (signature check). Empty file: 400.
- `GET /api/branding/logo` without a logo: 404.
- Email send never fails because of branding: any branding error falls back to plain text.

## Test Plan

1. Unit: `BrandingRulesTests` (colours, size boundary 2 MB, signatures, extensions), `BrandingServiceTests` (defaults, update, reset, upload replaces and deletes old, audit, validation errors), `BrandedEmailTests` (plain only without branding; HTML with colour, escaped text and linked logo).
2. Integration: `Branding/BrandingTests` (anonymous GET defaults and after PUT, 400 invalid colour, 400 logo > 2 MB / wrong type, logo round trip + headers, 403 admin / 401 anonymous on writes, audit entry), `Branding/BrandedEmailTests` (a notification email through the fake SMTP carries the HTML part and colour).
3. Client: `api/branding.test.ts`, `BrandingProvider.test.tsx` (sets and clears CSS variables, contrast foreground), `BrandingPage.test.tsx` (save, 400 shown, logo upload, hidden without permission), logo shown in sidebar and portal header.

## Verification Steps

1. `dotnet build` (0 warnings), filtered `dotnet test`; `dotnet ef migrations has-pending-model-changes` (no change expected).
2. `npx vitest run` for the touched client areas; at the end the full suites, `npm run build`, `npm run lint`.

## Done Criteria

- [ ] AC 1 logo + colours settable by the SuperAdmin only.
- [ ] AC 2 CSS variables applied at runtime from `GET /api/branding`.
- [ ] AC 3 invalid colour / oversize logo -> 400.
- [ ] AC 4 staff app, portal and emails branded.
