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
  categoriesManage: 'categories.manage',
  slaManage: 'sla.manage',
  channelsManage: 'channels.manage',
  reportsView: 'reports.view',
  auditView: 'audit.view',
  settingsManage: 'settings.manage',
} as const

export type Permission = (typeof permissions)[keyof typeof permissions]
