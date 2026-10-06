# 13-platform — plan overview

Entry point for the **platform** feature (Phase 3): organisation structure (departments, branches), branding and mobile support. Stories execute in order by their `NN` prefix; `NN` is the story number (Notion order).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 61 | [61-story-multi-department-CRM-61.md](61-story-multi-department-CRM-61.md) | Multi-department | CRM-61 | 12–20 (tickets, SLA), 06 (users) |

## Dependency notes

- **Data scope**: `IDataScope` + EF named query filters + a request middleware enforce visibility. Story 61 adds the department filter on tickets; story 62 extends the same scope with the branch (tickets and customers).
