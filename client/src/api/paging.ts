/** One page of a list (server: PagedResult<T>). */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

/** Query of a paged list endpoint (server: `search`, `page` default 1, `pageSize` default 20, max 100). */
export interface ListParams {
  search?: string
  page?: number
  pageSize?: number
}

/** `path` plus the query string of the given list parameters ("/api/customers?search=x&page=2"). */
export function listPath(path: string, { search, page, pageSize }: ListParams): string {
  const query = new URLSearchParams()
  if (search) query.set('search', search)
  if (page !== undefined) query.set('page', String(page))
  if (pageSize !== undefined) query.set('pageSize', String(pageSize))
  const queryString = query.toString()
  return queryString ? `${path}?${queryString}` : path
}
