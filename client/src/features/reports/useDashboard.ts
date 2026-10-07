import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '@/api/reports'
import { useReportBranch } from './report-branch'

/** How often the management dashboard reloads itself (the "real time" of CRM-49). */
export const DASHBOARD_REFRESH_MS = 30_000

/** GET /api/reports/dashboard, polled every `refreshMs` (default 30 s) and when the window gets focus again. */
export function useDashboard(refreshMs: number = DASHBOARD_REFRESH_MS) {
  const branchId = useReportBranch()
  return useQuery({
    queryKey: ['reports', 'dashboard', branchId],
    queryFn: ({ signal }) => (branchId ? getDashboard(signal, branchId) : getDashboard(signal)),
    refetchInterval: refreshMs,
    refetchOnWindowFocus: true,
  })
}
