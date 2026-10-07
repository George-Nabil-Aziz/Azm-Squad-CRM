import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useReportBranch, withBranch } from './report-branch'
import { getTicketReport, type TicketReportParams } from '@/api/reports'

/** GET /api/reports/tickets; the previous numbers stay visible while a new filter loads. */
export function useTicketReport(params: TicketReportParams) {
  const full = withBranch(params, useReportBranch())
  return useQuery({
    queryKey: ['reports', 'tickets', full],
    queryFn: ({ signal }) => getTicketReport(full, signal),
    placeholderData: keepPreviousData,
  })
}
