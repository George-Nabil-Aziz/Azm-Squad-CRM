import type { TFunction } from 'i18next'
import { z } from 'zod'
import {
  secretNames,
  type SecretName,
  type SystemSettings,
  type UpdateSettingsRequest,
  type WeekDay,
} from '@/api/settings'

const TIME = /^([01]\d|2[0-3]):[0-5]\d$/
const PREFIX = /^[A-Za-z0-9]{1,10}-?$/

/** Client-side checks of the settings form (the server validates again). Inputs are the raw text of the fields. */
export function createSettingsFormSchema(t: TFunction) {
  const port = z
    .string()
    .trim()
    .regex(/^\d+$/, t('settings.portInvalid'))
    .refine((value) => Number(value) >= 1 && Number(value) <= 65535, t('settings.portInvalid'))
  return z
    .object({
      enabled: z.boolean(),
      days: z.array(z.string()),
      start: z.string().regex(TIME, t('settings.timeInvalid')),
      end: z.string().regex(TIME, t('settings.timeInvalid')),
      timeZone: z.string().trim().min(1, t('settings.timeZoneRequired')),
      ticketPrefix: z.string().trim().regex(PREFIX, t('settings.prefixInvalid')),
      fromAddress: z.string().trim().refine((v) => v === '' || /^\S+@\S+\.\S+$/.test(v), t('settings.emailInvalid')),
      fromName: z.string(),
      smtpHost: z.string(),
      smtpPort: port,
      smtpSecurity: z.string(),
      smtpUserName: z.string(),
      imapHost: z.string(),
      imapPort: port,
      imapSecurity: z.string(),
      imapUserName: z.string(),
      imapFolder: z.string(),
      phoneNumberId: z.string(),
      // Secrets: empty = keep the stored value; they are never filled from the server.
      smtpPassword: z.string(),
      imapPassword: z.string(),
      whatsAppAccessToken: z.string(),
      whatsAppAppSecret: z.string(),
      whatsAppVerifyToken: z.string(),
      clearSecrets: z.array(z.string()),
    })
    .refine((values) => !values.enabled || values.days.length > 0, {
      message: t('settings.daysRequired'),
      path: ['days'],
    })
    .refine((values) => !TIME.test(values.start) || !TIME.test(values.end) || values.start < values.end, {
      message: t('settings.endBeforeStart'),
      path: ['end'],
    })
}

export type SettingsFormInput = z.input<ReturnType<typeof createSettingsFormSchema>>
export type SettingsFormValues = z.output<ReturnType<typeof createSettingsFormSchema>>

export function toFormValues(settings: SystemSettings): SettingsFormInput {
  const { businessHours, email } = settings
  return {
    enabled: businessHours.enabled,
    days: businessHours.days,
    start: businessHours.start,
    end: businessHours.end,
    timeZone: settings.timeZone,
    ticketPrefix: settings.ticketPrefix,
    fromAddress: email.fromAddress ?? '',
    fromName: email.fromName ?? '',
    smtpHost: email.smtpHost ?? '',
    smtpPort: String(email.smtpPort),
    smtpSecurity: email.smtpSecurity.toLowerCase(),
    smtpUserName: email.smtpUserName ?? '',
    imapHost: email.imapHost ?? '',
    imapPort: String(email.imapPort),
    imapSecurity: email.imapSecurity.toLowerCase(),
    imapUserName: email.imapUserName ?? '',
    imapFolder: email.imapFolder,
    phoneNumberId: settings.whatsApp.phoneNumberId ?? '',
    smtpPassword: '',
    imapPassword: '',
    whatsAppAccessToken: '',
    whatsAppAppSecret: '',
    whatsAppVerifyToken: '',
    clearSecrets: [],
  }
}

const orNull = (text: string) => (text.trim() === '' ? null : text.trim())

export function toRequest(values: SettingsFormValues): UpdateSettingsRequest {
  const secrets: Partial<Record<SecretName, string>> = {}
  for (const name of secretNames) {
    if (values.clearSecrets.includes(name)) secrets[name] = ''
    else if (values[name] !== '') secrets[name] = values[name]
  }
  return {
    businessHours: { enabled: values.enabled, days: values.days as WeekDay[], start: values.start, end: values.end },
    timeZone: values.timeZone,
    ticketPrefix: values.ticketPrefix,
    email: {
      fromAddress: orNull(values.fromAddress),
      fromName: orNull(values.fromName),
      smtpHost: orNull(values.smtpHost),
      smtpPort: Number(values.smtpPort),
      smtpSecurity: values.smtpSecurity,
      smtpUserName: orNull(values.smtpUserName),
      imapHost: orNull(values.imapHost),
      imapPort: Number(values.imapPort),
      imapSecurity: values.imapSecurity,
      imapUserName: orNull(values.imapUserName),
      imapFolder: values.imapFolder.trim() || 'INBOX',
    },
    whatsApp: { phoneNumberId: orNull(values.phoneNumberId) },
    ...(Object.keys(secrets).length > 0 ? { secrets } : {}),
  }
}

/** Server error key (ProblemDetails `errors`) → form field. */
export const serverErrorFields: Record<string, keyof SettingsFormInput> = {
  'businessHours.days': 'days',
  'businessHours.start': 'start',
  'businessHours.end': 'end',
  'businessHours.enabled': 'enabled',
  timeZone: 'timeZone',
  ticketPrefix: 'ticketPrefix',
  'email.fromAddress': 'fromAddress',
  'email.smtpPort': 'smtpPort',
  'email.imapPort': 'imapPort',
  'email.smtpSecurity': 'smtpSecurity',
  'email.imapSecurity': 'imapSecurity',
}
