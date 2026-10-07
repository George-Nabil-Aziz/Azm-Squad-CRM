import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useReportBranch, withBranch } from './report-branch'
import { getSlaReport, listSlaBreaches, type ReportRangeParams, type SlaBreachesParams } from '@/api/reports'

/** GET /api/reports/sla. */
export function useSlaReport(params: ReportRangeParams) {
  const full = withBranch(params, useReportBranch())
  return useQuery({
    queryKey: ['reports', 'sla', full],
    queryFn: ({ signal }) => getSlaReport(full, signal),
    placeholderData: keepPreviousData,
  })
}

/** One page of GET /api/reports/sla/breaches. */
export function useSlaBreaches(params: SlaBreachesParams) {
  const full = withBranch(params, useReportBranch())
  return useQuery({
    queryKey: ['reports', 'sla-breaches', full],
    queryFn: ({ signal }) => listSlaBreaches(full, signal),
    placeholderData: keepPreviousData,
  })
}
