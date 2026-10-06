import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { listCustomerAttachments, listCustomerNotes } from '@/api/customers'
import { customersQueryKey } from './useCustomers'

/** One page of a customer's notes (under the customers key prefix: customer changes reload it). */
export function useCustomerNotes(id: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: [...customersQueryKey, 'notes', id, page, pageSize],
    queryFn: ({ signal }) => listCustomerNotes(id, { page, pageSize }, signal),
    placeholderData: keepPreviousData,
  })
}

/** Every file of a customer. */
export function useCustomerAttachments(id: string) {
  return useQuery({
    queryKey: [...customersQueryKey, 'attachments', id],
    queryFn: ({ signal }) => listCustomerAttachments(id, signal),
  })
}
