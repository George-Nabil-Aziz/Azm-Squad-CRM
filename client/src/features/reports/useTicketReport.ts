import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getTicketReport, type TicketReportParams } from '@/api/reports'

/** GET /api/reports/tickets; the previous numbers stay visible while a new filter loads. */
export function useTicketReport(params: TicketReportParams) {
  return useQuery({
    queryKey: ['reports', 'tickets', params],
    queryFn: ({ signal }) => getTicketReport(params, signal),
    placeholderData: keepPreviousData,
  })
}
