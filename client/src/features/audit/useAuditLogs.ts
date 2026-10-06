import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { listAuditLogs, type AuditLogParams } from '@/api/audit-logs'

/** One page of GET /api/audit-logs; the previous page stays visible while the next one loads. */
export function useAuditLogs(params: AuditLogParams) {
  return useQuery({
    queryKey: ['audit-logs', params],
    queryFn: ({ signal }) => listAuditLogs(params, signal),
    placeholderData: keepPreviousData,
  })
}
