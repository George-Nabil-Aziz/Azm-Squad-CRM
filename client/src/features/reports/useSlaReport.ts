import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getSlaReport, listSlaBreaches, type ReportRangeParams, type SlaBreachesParams } from '@/api/reports'

/** GET /api/reports/sla. */
export function useSlaReport(params: ReportRangeParams) {
  return useQuery({
    queryKey: ['reports', 'sla', params],
    queryFn: ({ signal }) => getSlaReport(params, signal),
    placeholderData: keepPreviousData,
  })
}

/** One page of GET /api/reports/sla/breaches. */
export function useSlaBreaches(params: SlaBreachesParams) {
  return useQuery({
    queryKey: ['reports', 'sla-breaches', params],
    queryFn: ({ signal }) => listSlaBreaches(params, signal),
    placeholderData: keepPreviousData,
  })
}
