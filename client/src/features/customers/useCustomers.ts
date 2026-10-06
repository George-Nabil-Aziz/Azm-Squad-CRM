import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { listCustomers } from '@/api/customers'
import type { ListParams } from '@/api/paging'

/** Prefix of every customers query: mutations invalidate it so every page and search reloads. */
export const customersQueryKey = ['customers'] as const

/** One page of GET /api/customers. The previous page stays visible while the next one loads. */
export function useCustomers(params: ListParams) {
  return useQuery({
    queryKey: [...customersQueryKey, params],
    queryFn: ({ signal }) => listCustomers(params, signal),
    placeholderData: keepPreviousData,
  })
}
