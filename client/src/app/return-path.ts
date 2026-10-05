/** Router state that RequireAuth passes to /login: the page the user asked for. */
export interface LoginRedirectState {
  from: string
}

/**
 * Where to go after signing in: the stored in-app path, or "/" when it is missing or unsafe
 * (only same-origin paths starting with a single "/" are allowed; never /login itself).
 */
export function getReturnPath(state: unknown): string {
  const from = (state as Partial<LoginRedirectState> | null | undefined)?.from
  if (typeof from !== 'string') return '/'
  if (!from.startsWith('/') || from.startsWith('//') || from.startsWith('/\\')) return '/'
  if (from === '/login' || from.startsWith('/login?')) return '/'
  return from
}
