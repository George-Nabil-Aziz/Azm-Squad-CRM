import { useQuery } from '@tanstack/react-query'
import { getDashboardOverview } from '@/api/dashboard'
import { listTasks } from '@/api/tasks'
import { listTickets } from '@/api/tickets'

/** Prefix of every dashboard query. */
export const dashboardQueryKey = ['dashboard'] as const

/** GET /api/dashboard/overview, reloaded every minute. */
export function useDashboardOverview() {
  return useQuery({
    queryKey: [...dashboardQueryKey, 'overview'],
    queryFn: ({ signal }) => getDashboardOverview(signal),
    refetchInterval: 60_000,
  })
}

/** The newest tickets (GET /api/tickets, first page). */
export function useRecentTickets(count: number) {
  return useQuery({
    queryKey: [...dashboardQueryKey, 'recent-tickets', count],
    queryFn: ({ signal }) => listTickets({ page: 1, pageSize: count }, signal),
  })
}

/** My open tasks, the next due first (GET /api/tasks). */
export function useOpenTasks() {
  return useQuery({
    queryKey: [...dashboardQueryKey, 'tasks'],
    queryFn: ({ signal }) => listTasks('open', signal),
  })
}

/** `yyyy-MM-dd` (UTC) of `days - 1` days before `now`. */
export function utcDay(now: Date, daysBack = 0): string {
  return new Date(now.getTime() - daysBack * 86_400_000).toISOString().slice(0, 10)
}
