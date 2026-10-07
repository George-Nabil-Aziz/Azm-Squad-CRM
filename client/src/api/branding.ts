import { apiDelete, apiGetQuiet, apiPut, apiPutForm } from './client'

/** The branding (server: BrandingResponse). Null = the default theme / no logo. `logoUrl` carries a version (?v=). */
export interface Branding {
  primaryColor: string | null
  secondaryColor: string | null
  logoUrl: string | null
}

/** Body of "save colours": "#RGB" / "#RRGGBB"; an empty string goes back to the default colour. */
export interface UpdateBrandingRequest {
  primaryColor: string
  secondaryColor: string
}

/** GET /api/branding: public (login page and portal use it before anyone signs in); a failure is not shown to the user. */
export function getBranding(signal?: AbortSignal): Promise<Branding> {
  return apiGetQuiet<Branding>('/api/branding', signal)
}

/** PUT /api/branding (SuperAdmin): 400 on `primaryColor` / `secondaryColor` for an invalid colour. */
export function updateBranding(request: UpdateBrandingRequest): Promise<Branding> {
  return apiPut<Branding>('/api/branding', request)
}

/** PUT /api/branding/logo (multipart field "file"): PNG, JPG, WEBP or GIF up to 2 MB; 400 on `file` otherwise. */
export function uploadBrandingLogo(file: File): Promise<Branding> {
  const form = new FormData()
  form.append('file', file)
  return apiPutForm<Branding>('/api/branding/logo', form)
}

export function removeBrandingLogo(): Promise<Branding> {
  return apiDelete<Branding>('/api/branding/logo')
}

/** Largest logo the server accepts (2 MB). */
export const MAX_LOGO_BYTES = 2 * 1024 * 1024
