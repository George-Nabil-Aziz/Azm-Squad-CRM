import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getCsatReport, type ReportRangeParams } from '@/api/reports'

/** GET /api/reports/csat. */
export function useCsatReport(params: ReportRangeParams) {
  return useQuery({
    queryKey: ['reports', 'csat', params],
    queryFn: ({ signal }) => getCsatReport(params, signal),
    placeholderData: keepPreviousData,
  })
}
