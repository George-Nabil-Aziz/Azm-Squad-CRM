import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getCustomerTimeline, type TimelineParams } from '@/api/customers'
import { customersQueryKey } from './useCustomers'

/** One page of a customer's timeline. Under the customers key prefix, so every customer change reloads it. */
export function useCustomerTimeline(id: string, params: TimelineParams) {
  return useQuery({
    queryKey: [...customersQueryKey, 'timeline', id, params],
    queryFn: ({ signal }) => getCustomerTimeline(id, params, signal),
    placeholderData: keepPreviousData,
  })
}
