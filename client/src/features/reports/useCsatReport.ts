import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useReportBranch, withBranch } from './report-branch'
import { getCsatReport, type ReportRangeParams } from '@/api/reports'

/** GET /api/reports/csat. */
export function useCsatReport(params: ReportRangeParams) {
  const full = withBranch(params, useReportBranch())
  return useQuery({
    queryKey: ['reports', 'csat', full],
    queryFn: ({ signal }) => getCsatReport(full, signal),
    placeholderData: keepPreviousData,
  })
}
