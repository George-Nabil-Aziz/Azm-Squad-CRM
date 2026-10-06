import { apiGet, apiPost } from './client'

/** What the embedded form needs to render the captcha (server: WebFormConfigResponse). */
export interface WebFormConfig {
  captchaRequired: boolean
  captchaSiteKey: string | null
}

/** Body of POST /api/public/web-forms (server: WebFormRequest). `website` is the honeypot field: always empty for people. */
export interface WebFormInput {
  name: string
  email: string
  subject: string
  message: string
  captchaToken: string
  website: string
}

/** The ticket opened by a submission; `number` is "TKT-000001". */
export interface WebFormReceipt {
  number: string | null
}

export function getWebFormConfig(signal?: AbortSignal): Promise<WebFormConfig> {
  return apiGet<WebFormConfig>('/api/public/web-forms/config', signal)
}

/** 201 with the ticket number; 400 on the fields or `captchaToken`; 429 when sent too often (anonymous call). */
export function submitWebForm(input: WebFormInput): Promise<WebFormReceipt> {
  return apiPost<WebFormReceipt>('/api/public/web-forms', input)
}
