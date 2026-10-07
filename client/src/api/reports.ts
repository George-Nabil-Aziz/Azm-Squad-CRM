import type { TicketChannel, TicketPriority, TicketStatus } from '@/features/tickets/ticket-values'
import { apiGet, apiGetBlob } from './client'
import type { PagedResult } from './paging'

/** Export formats of a report (server: format=csv|xlsx). */
export type ExportFormat = 'csv' | 'xlsx'

/** Date range of a report: whole UTC days `yyyy-MM-dd`; missing = the server's default (last 30 days). */
export interface ReportRangeParams {
  from?: string
  to?: string
  /** Only the tickets of this branch (CRM-62). */
  branchId?: string
}

/** `path` plus the query string of the parameters that are set. */
export function reportPath(path: string, params: object): string {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') query.set(key, String(value))
  }
  const queryString = query.toString()
  return queryString ? `${path}?${queryString}` : path
}

export interface TicketReportParams extends ReportRangeParams {
  status?: TicketStatus
  categoryId?: string
  channel?: TicketChannel
  priority?: TicketPriority
}

export interface ReportCount<K extends string = string> {
  key: K
  count: number
}

export interface CategoryCount {
  /** null = tickets without a category. */
  categoryId: string | null
  name: string | null
  count: number
}

export interface DailyCount {
  date: string
  count: number
}

/** GET /api/reports/tickets (server: TicketReportResponse). */
export interface TicketReport {
  from: string
  to: string
  total: number
  byStatus: ReportCount<TicketStatus>[]
  byCategory: CategoryCount[]
  byChannel: ReportCount<TicketChannel>[]
  byPriority: ReportCount<TicketPriority>[]
  byDay: DailyCount[]
}

export function getTicketReport(params: TicketReportParams, signal?: AbortSignal): Promise<TicketReport> {
  return apiGet<TicketReport>(reportPath('/api/reports/tickets', params), signal)
}

/** The report as a file (authorized download); the same filters as the report. */
export function exportTicketReport(params: TicketReportParams, format: ExportFormat): Promise<Blob> {
  return apiGetBlob(reportPath('/api/reports/tickets/export', { ...params, format }))
}

export interface SlaTargetStats {
  met: number
  breached: number
  pending: number
  /** null = nothing decided yet. */
  compliancePercent: number | null
  /** Minutes; null = no ticket has a result yet. */
  averageMinutes: number | null
}

export interface SlaPriorityRow {
  priority: TicketPriority | 'all'
  tickets: number
  response: SlaTargetStats
  resolution: SlaTargetStats
}

/** GET /api/reports/sla (server: SlaReportResponse). */
export interface SlaReport {
  from: string
  to: string
  overall: SlaPriorityRow
  priorities: SlaPriorityRow[]
}

export interface BreachedTicket {
  ticketId: string
  number: string
  subject: string
  priority: TicketPriority
  assigneeName: string | null
  createdAt: string
  responseDueAt: string | null
  firstResponseAt: string | null
  resolutionDueAt: string | null
  resolvedAt: string | null
  responseBreached: boolean
  resolutionBreached: boolean
}

export interface SlaBreachesParams extends ReportRangeParams {
  page?: number
  pageSize?: number
}

export function getSlaReport(params: ReportRangeParams, signal?: AbortSignal): Promise<SlaReport> {
  return apiGet<SlaReport>(reportPath('/api/reports/sla', params), signal)
}

export function listSlaBreaches(params: SlaBreachesParams, signal?: AbortSignal): Promise<PagedResult<BreachedTicket>> {
  return apiGet<PagedResult<BreachedTicket>>(reportPath('/api/reports/sla/breaches', params), signal)
}

export interface CsatGroup {
  /** null = ratings of tickets without an agent / category. */
  id: string | null
  name: string | null
  averageRating: number
  count: number
}

export interface LowRating {
  ticketId: string
  ticketNumber: string
  rating: number
  comment: string | null
  ratedAt: string
  agentName: string | null
}

/** GET /api/reports/csat (server: CsatReportResponse). */
export interface CsatReport {
  from: string
  to: string
  totalRatings: number
  averageRating: number | null
  distribution: { rating: number; count: number }[]
  byDay: { date: string; averageRating: number | null; count: number }[]
  byAgent: CsatGroup[]
  byCategory: CsatGroup[]
  lowRatings: LowRating[]
  surveysSent: number
  responseRatePercent: number | null
}

export function getCsatReport(params: ReportRangeParams, signal?: AbortSignal): Promise<CsatReport> {
  return apiGet<CsatReport>(reportPath('/api/reports/csat', params), signal)
}

export interface AgentPerformance {
  agentId: string
  name: string
  ticketsHandled: number
  averageFirstResponseMinutes: number | null
  averageResolutionMinutes: number | null
  slaPercent: number | null
  averageCsat: number | null
  csatCount: number
}

/** GET /api/reports/agents (server: AgentReportResponse). */
export interface AgentReport {
  from: string
  to: string
  agents: AgentPerformance[]
}

export function getAgentReport(params: ReportRangeParams, signal?: AbortSignal): Promise<AgentReport> {
  return apiGet<AgentReport>(reportPath('/api/reports/agents', params), signal)
}

export function exportAgentReport(params: ReportRangeParams, format: ExportFormat): Promise<Blob> {
  return apiGetBlob(reportPath('/api/reports/agents/export', { ...params, format }))
}

/** GET /api/reports/dashboard (server: DashboardResponse). */
export interface Dashboard {
  generatedAt: string
  openTickets: number
  breachedToday: number
  averageResponseMinutes: number | null
  averageCsat: number | null
  csatCount: number
  ticketsPerDay: DailyCount[]
  ticketsByChannel: ReportCount<TicketChannel>[]
}

export function getDashboard(signal?: AbortSignal, branchId?: string): Promise<Dashboard> {
  return apiGet<Dashboard>(reportPath('/api/reports/dashboard', { branchId }), signal)
}
