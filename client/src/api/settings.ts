import { apiGet, apiPut } from './client'

/** Day names the server uses in business hours (index = JavaScript / .NET DayOfWeek). */
export const weekDays = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'] as const
export type WeekDay = (typeof weekDays)[number]

/** SMTP / IMAP security modes (the server reads them case-insensitively). Labels: t(`settings.securityModes.${mode}`). */
export const securityModes = ['none', 'auto', 'starttls', 'sslonconnect'] as const

export interface BusinessHours {
  enabled: boolean
  days: WeekDay[]
  /** "HH:mm" in the configured time zone. */
  start: string
  end: string
}

export interface EmailSettings {
  fromAddress: string | null
  fromName: string | null
  smtpHost: string | null
  smtpPort: number
  smtpSecurity: string
  smtpUserName: string | null
  imapHost: string | null
  imapPort: number
  imapSecurity: string
  imapUserName: string | null
  imapFolder: string
}

/** Which secrets have a stored value. The values themselves are never returned. */
export interface SecretsSet {
  smtpPassword: boolean
  imapPassword: boolean
  whatsAppAccessToken: boolean
  whatsAppAppSecret: boolean
  whatsAppVerifyToken: boolean
}

export type SecretName = keyof SecretsSet

export const secretNames: readonly SecretName[] = [
  'smtpPassword',
  'imapPassword',
  'whatsAppAccessToken',
  'whatsAppAppSecret',
  'whatsAppVerifyToken',
]

/** GET /api/settings (needs settings.manage). */
export interface SystemSettings {
  businessHours: BusinessHours
  timeZone: string
  ticketPrefix: string
  email: EmailSettings
  whatsApp: { phoneNumberId: string | null }
  secretsSet: SecretsSet
}

/** Secrets of a PUT: a missing key keeps the stored value, "" clears it, text replaces it. */
export type SecretsUpdate = Partial<Record<SecretName, string>>

export interface UpdateSettingsRequest {
  businessHours: BusinessHours
  timeZone: string
  ticketPrefix: string
  email: EmailSettings
  whatsApp: { phoneNumberId: string | null }
  secrets?: SecretsUpdate
}

export function getSettings(signal?: AbortSignal): Promise<SystemSettings> {
  return apiGet<SystemSettings>('/api/settings', signal)
}

export function updateSettings(request: UpdateSettingsRequest): Promise<SystemSettings> {
  return apiPut<SystemSettings>('/api/settings', request)
}
