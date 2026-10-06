import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getAgentReport, type ReportRangeParams } from '@/api/reports'

/** GET /api/reports/agents. */
export function useAgentReport(params: ReportRangeParams) {
  return useQuery({
    queryKey: ['reports', 'agents', params],
    queryFn: ({ signal }) => getAgentReport(params, signal),
    placeholderData: keepPreviousData,
  })
}
