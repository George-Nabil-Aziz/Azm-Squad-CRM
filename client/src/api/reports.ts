import type { TicketChannel, TicketPriority, TicketStatus } from '@/features/tickets/ticket-values'
import { apiGet, apiGetBlob } from './client'

/** Export formats of a report (server: format=csv|xlsx). */
export type ExportFormat = 'csv' | 'xlsx'

/** Date range of a report: whole UTC days `yyyy-MM-dd`; missing = the server's default (last 30 days). */
export interface ReportRangeParams {
  from?: string
  to?: string
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
