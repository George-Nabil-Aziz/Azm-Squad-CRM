# 13-platform — plan overview

Entry point for the **platform** feature (Phase 3): organisation structure (departments, branches), branding and mobile support. Stories execute in order by their `NN` prefix; `NN` is the story number (Notion order).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 61 | [61-story-multi-department-CRM-61.md](61-story-multi-department-CRM-61.md) | Multi-department | CRM-61 | 12–20 (tickets, SLA), 06 (users) |
| 62 | [62-story-multi-branch-CRM-62.md](62-story-multi-branch-CRM-62.md) | Multi-branch | CRM-62 | 61 (data scope), 08–11 (customers), 45–49 (reports) |
| 63 | [63-story-custom-branding-CRM-63.md](63-story-custom-branding-CRM-63.md) | Custom branding (colors & logo) | CRM-63 | 35 (system settings), 11 (file storage), 23 (email), 40 (portal) |
| 64 | [64-story-mobile-responsive-CRM-64.md](64-story-mobile-responsive-CRM-64.md) | Mobile responsive | CRM-64 | 01 (client shell), 09-customer-portal |

## Dependency notes

- **Data scope**: `IDataScope` + EF named query filters + a request middleware enforce visibility. Story 61 adds the department filter on tickets; story 62 extends the same scope with the branch (tickets and customers).
