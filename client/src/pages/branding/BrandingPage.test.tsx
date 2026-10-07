import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getBranding, removeBrandingLogo, updateBranding, uploadBrandingLogo, type Branding } from '@/api/branding'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { BrandingProvider } from '@/features/branding/BrandingProvider'
import { BrandingPage } from './BrandingPage'

vi.mock('@/api/branding', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/branding')>()),
  getBranding: vi.fn(),
  updateBranding: vi.fn(),
  uploadBrandingLogo: vi.fn(),
  removeBrandingLogo: vi.fn(),
}))

const plain: Branding = { primaryColor: null, secondaryColor: null, logoUrl: null }

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <BrandingProvider>
        <BrandingPage />
      </BrandingProvider>
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

describe('BrandingPage', () => {
  beforeEach(() => {
    document.documentElement.removeAttribute('style')
    vi.mocked(getBranding).mockReset().mockResolvedValue(plain)
    vi.mocked(updateBranding).mockReset()
    vi.mocked(uploadBrandingLogo).mockReset()
    vi.mocked(removeBrandingLogo).mockReset()
  })

  it('starts from the saved colours', async () => {
    vi.mocked(getBranding).mockResolvedValue({ ...plain, primaryColor: '#0a5cad' })
    renderPage()

    await waitFor(() => expect(screen.getByLabelText('Primary colour')).toHaveValue('#0a5cad'))
    expect(screen.getByRole('heading', { level: 1, name: 'Branding' })).toBeInTheDocument()
  })

  it('saves the colours and applies them at once', async () => {
    vi.mocked(updateBranding).mockResolvedValue({ primaryColor: '#0a2540', secondaryColor: '#ffeb3b', logoUrl: null })
    renderPage()
    await waitFor(() => expect(getBranding).toHaveBeenCalled())

    fireEvent.change(screen.getByLabelText('Primary colour'), { target: { value: ' #0a2540 ' } })
    fireEvent.change(screen.getByLabelText('Secondary colour'), { target: { value: '#ffeb3b' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save colours' }))

    await waitFor(() => expect(updateBranding).toHaveBeenCalledWith({ primaryColor: '#0a2540', secondaryColor: '#ffeb3b' }))
    expect(await screen.findByText('The colours were saved.')).toBeInTheDocument()
    await waitFor(() => expect(document.documentElement.style.getPropertyValue('--primary')).toBe('#0a2540'))
  })

  it('shows the server message under the colour that is invalid', async () => {
    vi.mocked(updateBranding).mockRejectedValue(
      new ApiError('PUT /api/branding failed with status 400', 400, {
        status: 400,
        errors: { primaryColor: ['Enter a colour like #0a5cad (3 or 6 hex digits).'] },
      }),
    )
    renderPage()
    await waitFor(() => expect(getBranding).toHaveBeenCalled())

    fireEvent.change(screen.getByLabelText('Primary colour'), { target: { value: 'blue' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save colours' }))

    expect(await screen.findByText('Enter a colour like #0a5cad (3 or 6 hex digits).')).toBeInTheDocument()
    expect(screen.getByLabelText('Primary colour')).toHaveAttribute('aria-invalid', 'true')
  })

  it('uploads a logo and shows it', async () => {
    vi.mocked(uploadBrandingLogo).mockResolvedValue({ ...plain, logoUrl: '/api/branding/logo?v=2' })
    renderPage()
    await waitFor(() => expect(getBranding).toHaveBeenCalled())

    const file = new File(['x'], 'logo.png', { type: 'image/png' })
    fireEvent.change(screen.getByLabelText('Logo file'), { target: { files: [file] } })

    await waitFor(() => expect(uploadBrandingLogo).toHaveBeenCalledWith(file))
    expect(await screen.findByText('The logo was saved.')).toBeInTheDocument()
    expect((await screen.findAllByRole('img', { name: 'Company logo' }))[0]).toHaveAttribute('src', '/api/branding/logo?v=2')
  })

  it('refuses a logo over 2 MB before calling the API', async () => {
    renderPage()
    await waitFor(() => expect(getBranding).toHaveBeenCalled())

    const big = new File(['x'], 'big.png', { type: 'image/png' })
    Object.defineProperty(big, 'size', { value: 2 * 1024 * 1024 + 1 })
    fireEvent.change(screen.getByLabelText('Logo file'), { target: { files: [big] } })

    expect(await screen.findByText('The logo must be 2 MB or smaller.')).toBeInTheDocument()
    expect(uploadBrandingLogo).not.toHaveBeenCalled()
  })

  it('removes the logo', async () => {
    vi.mocked(getBranding).mockResolvedValue({ ...plain, logoUrl: '/api/branding/logo?v=1' })
    vi.mocked(removeBrandingLogo).mockResolvedValue(plain)
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Remove the logo' }))

    await waitFor(() => expect(removeBrandingLogo).toHaveBeenCalled())
    expect(await screen.findByText('The logo was removed.')).toBeInTheDocument()
  })
})
