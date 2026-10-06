# Story intake

- Folder: `.squad/stories/07-agent-dashboard/CRM-31/intake.md`

---

## Feature

- **Feature name (display):** Agent dashboard
- **Feature slug (folder under `plans/`):** `07-agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none` (stories live in Notion database "CRM User Stories")
- **Work item id:** `CRM-31`
- **Work item type:** `User Story`
- **Status:** `Ready`
- **Assignee:** `George Nabil`
- **Labels:** `Phase 2`, `Priority: Mid`

---

## Title

```
Tasks & reminders
```

---

## Description

```
As an agent, I want to create tasks with due dates (optionally linked to a ticket) and get reminders, so that I never forget a follow-up.
```

---

## Acceptance criteria

```
1. Creating a task with title and due date returns 201.
2. A due date in the past returns 400.
3. A reminder notification is sent at the due time.
4. Marking a task Done removes it from my open tasks.
```

---

## Attachments

None.

---

## Dependencies

- CRM-28 (notifications: `INotificationDispatcher`, `TaskReminder` type, Hangfire jobs from CRM-21), CRM-13 (optional ticket link).

## Extra notes (optional)

- Tests first (TDD).

## Technical hints (optional)

- CRM 01 reference (read only): `specs/19-tasks-reminders/`.
- Reminder = plain job class `TaskReminderJob` registered as a Hangfire recurring job (not started in Testing); tests call it directly with a fake clock.

## Out of scope

- Recurring tasks, assigning tasks to other users, editing tasks, task comments.
