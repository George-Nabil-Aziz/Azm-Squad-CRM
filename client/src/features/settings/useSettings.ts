import { useQuery } from '@tanstack/react-query'
import { getSettings } from '@/api/settings'

/** Key of the settings query: saving invalidates it. */
export const settingsQueryKey = ['settings'] as const

/** GET /api/settings. */
export function useSettings() {
  return useQuery({
    queryKey: settingsQueryKey,
    queryFn: ({ signal }) => getSettings(signal),
  })
}
