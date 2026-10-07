import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useReportBranch, withBranch } from './report-branch'
import { getAgentReport, type ReportRangeParams } from '@/api/reports'

/** GET /api/reports/agents. */
export function useAgentReport(params: ReportRangeParams) {
  const full = withBranch(params, useReportBranch())
  return useQuery({
    queryKey: ['reports', 'agents', full],
    queryFn: ({ signal }) => getAgentReport(full, signal),
    placeholderData: keepPreviousData,
  })
}
