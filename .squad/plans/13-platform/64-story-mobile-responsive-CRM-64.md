# Story 64 — Mobile responsive (Story: CRM-64)

## Prerequisites

- shadcn `sidebar` (already collapses into a sheet below 768 px through `useIsMobile`), `components/ui/table.tsx`, `AppLayout`, `AppHeader`, `PortalLayout`.
- No backend change, no migration, no new permission. Reference only: CRM 01 `specs/54-web-mobile-friendly`.

## Story Goal

1. **AC 1** — Every main screen is usable at 375 px with no horizontal page scroll: tables become cards (AC 3), the header shrinks (user name hidden below `sm`, sign-out keeps its icon with a screen-reader label), no fixed pixel width wider than a phone, wide content scrolls inside its own container.
2. **AC 2** — The sidebar collapses into a menu: below 768 px the navigation lives in the sheet opened by the header button and closes after choosing a page (shadcn behaviour, now covered by a test).
3. **AC 3** — `Table` (shadcn) is **responsive by default**: from `md` up a normal table; below it every row is a bordered card and each cell shows its column title before its value. The titles are copied from the header row into `data-label` by a layout effect inside `Table` (no change in the ~20 tables that use it); the header stays in the DOM for screen readers (`sr-only`). `responsive={false}` opts a table out.
4. **AC 4** — Only logical classes are used (`ms-`, `me-`, `ps-`, `pe-`, `start-`, `end-`, `text-start`, `before:text-start`); the sheet opens on the right in Arabic. A source-scanning test fails on any direction-specific Tailwind class or fixed pixel width > 340 px in components.

## Context — Read These Files First

1. `client/src/components/ui/table.tsx`, `client/src/components/ui/sidebar.tsx` (`useSidebar`, mobile sheet), `client/src/hooks/use-mobile.ts`.
2. `client/src/components/layout/AppHeader.tsx`, `AppLayout.tsx`, `components/portal/PortalLayout.tsx`.
3. `client/src/App.layout.test.tsx`, `client/src/test/setup.ts` (matchMedia / innerWidth), `test/fake-api.ts`.

## Frontend Tasks

- `table.tsx`: `ResponsiveContext`, `labelCells`, `max-md:` classes on table / thead / tbody / tr / td, `responsive` prop.
- `AppHeader.tsx`: `max-sm:hidden` on the name (truncate), `max-sm:sr-only` on the sign-out text.
- Tests as below. No new strings (no i18n change).

## Edge Cases & Failure Modes

- Cells without a header title (an actions column) show the content only.
- Rows added or filtered later are labelled again after each render of `Table`.
- A table with `colSpan` cells gets labels by child index (colspan rows are rare; they simply show a title or none).
- jsdom has no layout: the tests check behaviour that does not need it (menu in the sheet, `data-label`, classes of the card layout, RTL side) and the source guards; real width is checked in the browser at 375 px.

## Test Plan

1. `components/ui/table.test.tsx`: labels, card classes below `md`, header kept for screen readers, logical classes only, opt-out, rows that arrive later.
2. `App.mobile.test.tsx` (375 px): navigation hidden until the menu button, sheet closes after navigating, Arabic: sheet on the right with the Arabic navigation and `dir="rtl"`, header keeps sign-out.
3. `responsive-rtl.test.ts`: no direction-specific Tailwind classes and no wide fixed pixel widths in components.

## Verification Steps

1. `npx vitest run` for the touched areas; at the end the full suites, `npm run build`, `npm run lint`.

## Done Criteria

- [ ] AC 1 no horizontal scroll at 375 px (tables are cards, header fits).
- [ ] AC 2 sidebar becomes a menu.
- [ ] AC 3 tables switch to cards.
- [ ] AC 4 works in RTL.
