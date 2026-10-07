import { apiGet } from './client'

/** A ticket past an SLA due time that is still open (server: OverdueTicketResponse). */
export interface OverdueTicket {
  ticketId: string
  /** "TKT-000001". */
  number: string
  subject: string
  priority: 'high' | 'mid' | 'low'
  assigneeName: string | null
  dueAt: string
}

/** GET /api/dashboard/overview (server: DashboardOverviewResponse): counts of all tickets; `totalCustomers` is null without customers.view. */
export interface DashboardOverview {
  generatedAt: string
  openTickets: number
  pendingTickets: number
  breachedNow: number
  resolvedToday: number
  newToday: number
  totalCustomers: number | null
  overdue: OverdueTicket[]
}

export function getDashboardOverview(signal?: AbortSignal): Promise<DashboardOverview> {
  return apiGet<DashboardOverview>('/api/dashboard/overview', signal)
}

export interface NamedCount {
  key: string
  count: number
}

export interface ActivityEntry {
  id: number
  occurredAt: string
  userEmail: string | null
  action: string
  entityType: string
  entityId: string | null
}

/** GET /api/dashboard/system-overview (server: SystemOverviewResponse): counts of every module; SuperAdmin and Admin only. */
export interface SystemOverview {
  generatedAt: string
  customers: number
  ticketsByStatus: NamedCount[]
  slaBreachedNow: number
  chats: { waiting: number; active: number; today: number }
  tasks: { open: number; overdue: number }
  quickReplies: { personal: number; shared: number }
  kb: { publishedArticles: number; draftArticles: number; publishedFaqs: number; draftFaqs: number }
  portal: { surveysSent: number; surveysAnswered: number; accounts: number }
  users: { byRole: NamedCount[]; active: number; activeAgents: number; onDutyAgents: number }
  departments: { active: number; total: number }
  branches: { active: number; total: number }
  webFormSubmissions: number
  messages: { channel: 'email' | 'whatsapp' | 'sms'; sent: number; failed: number; received: number }[]
  apiKeys: { active: number; total: number }
  webhooks: { enabled: number; total: number; failedDeliveries: number }
  erp: { configured: boolean; synced: number; failed: number }
  ai: { configured: boolean }
  /** null without audit.view. */
  recentActivity: ActivityEntry[] | null
}

export function getSystemOverview(signal?: AbortSignal): Promise<SystemOverview> {
  return apiGet<SystemOverview>('/api/dashboard/system-overview', signal)
}
