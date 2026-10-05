# foundation — plan overview

Entry point for the **foundation** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-project-skeleton.md](01-story-project-skeleton.md) | Project skeleton (.NET API + React) | CRM-1 | — |
| 02 | [02-story-global-error-handling.md](02-story-global-error-handling.md) | Global error handling & validation | CRM-5 | 01 |

## Dependency notes

- **Story 01 (CRM-1)** creates `server/` and `client/`; every other story in every feature depends on it.
- Shared contract: the API runs on `http://localhost:5080` (launchSettings `http` profile) and the Vite proxy in `client/vite.config.ts` points to it. Change both together.
- **Story 02 (CRM-5)** defines the error contract every later story uses: ProblemDetails with `correlationId`, `X-Correlation-Id` header, Application exceptions (`ValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException`) + `ValidateOrThrowAsync`, the shared `CrmApiFactory` test host, and the client `ApiError` / `onApiError` → sonner toast. CRM-3 swaps the sonner `Toaster` for the shadcn wrapper (one import); CRM-4 moves `client/src/api/error-messages.ts` to i18n.
