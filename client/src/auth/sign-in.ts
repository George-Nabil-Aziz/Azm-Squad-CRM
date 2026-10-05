import { login } from '../api/auth'
import { clearSession, saveSession } from './session'

/** Calls POST /api/auth/login and stores the token. Rejects with ApiError (401 = wrong email or password). */
export async function signIn(email: string, password: string): Promise<void> {
  const response = await login({ email, password })
  saveSession(response.accessToken, response.expiresAt)
}

export function signOut(): void {
  clearSession()
}
