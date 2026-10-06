import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '@/api/reports'

/** How often the management dashboard reloads itself (the "real time" of CRM-49). */
export const DASHBOARD_REFRESH_MS = 30_000

/** GET /api/reports/dashboard, polled every `refreshMs` (default 30 s) and when the window gets focus again. */
export function useDashboard(refreshMs: number = DASHBOARD_REFRESH_MS) {
  return useQuery({
    queryKey: ['reports', 'dashboard'],
    queryFn: ({ signal }) => getDashboard(signal),
    refetchInterval: refreshMs,
    refetchOnWindowFocus: true,
  })
}
