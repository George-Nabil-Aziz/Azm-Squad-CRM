import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getCustomer, listCustomers } from '@/api/customers'
import type { ListParams } from '@/api/paging'

/** Prefix of every customers query: mutations invalidate it so every page, search and customer reloads. */
export const customersQueryKey = ['customers'] as const

/** One page of GET /api/customers. The previous page stays visible while the next one loads. */
export function useCustomers(params: ListParams) {
  return useQuery({
    queryKey: [...customersQueryKey, params],
    queryFn: ({ signal }) => listCustomers(params, signal),
    placeholderData: keepPreviousData,
  })
}

/** One customer with all its contacts (GET /api/customers/{id}). */
export function useCustomer(id: string) {
  return useQuery({
    queryKey: [...customersQueryKey, 'detail', id],
    queryFn: ({ signal }) => getCustomer(id, signal),
  })
}
