# Story 38 — Knowledge base: search (Story: CRM-38)

## Prerequisites

- Stories 36 and 37 done (articles, FAQs, `PortalKbEndpoints`).
- CRM 01 reference (read-only): `specs/28-search-knowledge-base/`.

## Story Goal

1. `GET /api/kb/search?q=` (`kb.view`) returns matching **articles and FAQs ranked by relevance** (AC 1); the same service backs the anonymous portal search `GET /api/portal/kb/search?q=` (CRM-43).
2. **Arabic normalization** on both sides: أ إ آ ٱ → ا, ة → ه (also ى → ي, diacritics and tatweel removed, Arabic-Indic digits → ASCII, lower-case) (AC 2).
3. **Drafts / unpublished content is never returned** (AC 3).
4. An **empty or blank query returns an empty list** with 200 (AC 4).
5. Client: a search box on the knowledge base page (agents and editors) listing results with type, title and snippet.

## Design

- **Domain** `KbSearchText.Normalize(string?)` (pure, unit tested). `KbArticle` / `KbFaq` keep a stored `SearchText` (normalized, both languages; rebuilt on every create / update) so the repository can match with `LIKE` on SQL Server and SQLite.
- **Application** `IKbSearchService.SearchAsync(q)`: normalizes and splits the query into terms (max 8, each ≤ 50 chars); the repository returns **published, non-deleted** candidates containing **all** terms (max 200); the service scores in memory: per term title match +10, body / answer match +3 (+1 per extra occurrence up to 3), full normalized phrase in the title +20; ties → newest first; at most 50 results. Result: `{ type: "article" | "faq", id, title, snippet, score }` in the request language (other language as fallback; snippet = first 160 characters of body / answer).
- **API** `GET /api/kb/search` (`kb.view`), `GET /api/portal/kb/search` (anonymous).
- **Migration** `AddKbSearchText`.

## Backend Tasks

1. Tests first: unit `KbSearchTextTests` (each AC-2 mapping, diacritics, tatweel, digits, whitespace, null), `KbSearchServiceTests` (title beats body; all terms required; phrase bonus; Arabic query without hamza finds the hamza article and ة/ه; empty / whitespace query → empty and the repository is not called; limit of 50), integration `KbSearchTests` (published article and FAQ found and ranked; draft article and unpublished FAQ never returned; Arabic normalization end to end; empty query → 200 `[]`; agent allowed; anonymous staff search 401; portal search anonymous).
2. Implement domain normalizer + `SearchText` on both entities, repository `IKbSearchRepository`, service, endpoints, migration.

## Frontend Tasks

1. Tests first: typing in the search box (Enter / button) calls the API and lists results; empty box lists nothing and does not call; "no results" message.
2. `searchKb` in `api/knowledge-base.ts`, `KbSearchPanel` on the page (above the tabs), i18n en + ar.

## Edge cases

- `%`, `_` and `\` in the query are matched literally. Terms with only punctuation after normalization are dropped. An article with only an Arabic version is found by an Arabic query; a mixed query "refund استرداد" needs both terms in the same item.

## Verification / done

All build / test / lint commands green; AC 1–4 each have a test.
