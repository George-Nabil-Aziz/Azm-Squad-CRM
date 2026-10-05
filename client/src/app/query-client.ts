import { QueryClient } from '@tanstack/react-query'

/** One QueryClient per App instance (tests get a fresh cache for every render). */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Every failed call already shows a toast (ApiErrorToaster); a retry would show it again.
        retry: false,
        staleTime: 30_000,
      },
    },
  })
}
