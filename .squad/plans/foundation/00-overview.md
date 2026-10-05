# foundation — plan overview

Entry point for the **foundation** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-project-skeleton.md](01-story-project-skeleton.md) | Project skeleton (.NET API + React) | CRM-1 | — |

## Dependency notes

- **Story 01 (CRM-1)** creates `server/` and `client/`; every other story in every feature depends on it.
- Shared contract: the API runs on `http://localhost:5080` (launchSettings `http` profile) and the Vite proxy in `client/vite.config.ts` points to it. Change both together.
