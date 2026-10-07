/**
 * Permission names, the same as the server catalogue (Crm.Application.Auth.Permissions; permissions.test.ts keeps
 * both lists equal). GET /api/auth/me returns the signed-in user's permissions; the UI only hides what the user
 * may not use — the API enforces every permission itself.
 */
export const permissions = {
  usersManage: 'users.manage',
  usersManageSuperAdmins: 'users.manage-super-admins',
  customersView: 'customers.view',
  customersManage: 'customers.manage',
  ticketsView: 'tickets.view',
  ticketsManage: 'tickets.manage',
  ticketsAssign: 'tickets.assign',
  notificationsView: 'notifications.view',
  tasksManage: 'tasks.manage',
  quickRepliesManageShared: 'quick-replies.manage-shared',
  categoriesManage: 'categories.manage',
  slaManage: 'sla.manage',
  channelsManage: 'channels.manage',
  reportsView: 'reports.view',
  kbView: 'kb.view',
  kbManage: 'kb.manage',
  auditView: 'audit.view',
  settingsManage: 'settings.manage',
  integrationsManage: 'integrations.manage',
  chatHandle: 'chat.handle',
  departmentsManage: 'departments.manage',
  branchesManage: 'branches.manage',
} as const

export type Permission = (typeof permissions)[keyof typeof permissions]
