import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Largest time the server accepts (one year, server: SlaPolicy.MaxMinutes). */
export const SLA_MAX_MINUTES = 525_600

/** Client-side checks of the SLA dialog (the server validates again). Inputs are the raw text of the fields. */
export function createSlaPolicyFormSchema(t: TFunction) {
  const minutes = z
    .string()
    .trim()
    .regex(/^\d+$/, t('sla.minutesInvalid'))
    .transform(Number)
    .pipe(z.number().min(1, t('sla.minutesInvalid')).max(SLA_MAX_MINUTES, t('sla.minutesTooLarge')))
  return z
    .object({ responseMinutes: minutes, resolutionMinutes: minutes })
    .refine((values) => values.resolutionMinutes >= values.responseMinutes, {
      message: t('sla.resolutionBelowResponse'),
      path: ['resolutionMinutes'],
    })
}

export type SlaPolicyFormInput = z.input<ReturnType<typeof createSlaPolicyFormSchema>>
export type SlaPolicyFormValues = z.output<ReturnType<typeof createSlaPolicyFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const slaPolicyFormFields = ['responseMinutes', 'resolutionMinutes'] as const
