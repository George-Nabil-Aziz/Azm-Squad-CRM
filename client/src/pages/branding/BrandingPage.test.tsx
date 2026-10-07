import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getBranding, removeBrandingLogo, updateBranding, uploadBrandingLogo, type Branding } from '@/api/branding'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { BrandingProvider } from '@/features/branding/BrandingProvider'
import { i18n } from '@/i18n/i18n'
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

  describe('colour picker', () => {
    it('sets the colour when a preset swatch is clicked and keeps the hex field and picker in sync', async () => {
      renderPage()
      await waitFor(() => expect(getBranding).toHaveBeenCalled())
      const presets = within(screen.getByRole('group', { name: 'Primary colour presets' }))
      expect(presets.getAllByRole('button')).toHaveLength(12)

      fireEvent.click(presets.getByRole('button', { name: 'Teal' }))

      expect(screen.getByLabelText('Primary colour')).toHaveValue('#0d9488')
      expect(screen.getByLabelText('Choose Primary colour with the colour picker')).toHaveValue('#0d9488')
      expect(presets.getByRole('button', { name: 'Teal' })).toHaveAttribute('aria-pressed', 'true')
      expect(presets.getByRole('button', { name: 'Blue' })).toHaveAttribute('aria-pressed', 'false')
      // The secondary colour is independent.
      expect(screen.getByLabelText('Secondary colour')).toHaveValue('')
    })

    it('follows the native picker and the hex field', async () => {
      renderPage()
      await waitFor(() => expect(getBranding).toHaveBeenCalled())

      fireEvent.change(screen.getByLabelText('Choose Secondary colour with the colour picker'), { target: { value: '#ff8800' } })
      expect(screen.getByLabelText('Secondary colour')).toHaveValue('#ff8800')

      fireEvent.change(screen.getByLabelText('Secondary colour'), { target: { value: '#ABC' } })
      expect(screen.getByLabelText('Choose Secondary colour with the colour picker')).toHaveValue('#aabbcc')
    })

    it('saves a preset colour', async () => {
      vi.mocked(updateBranding).mockResolvedValue({ ...plain, primaryColor: '#dc2626' })
      renderPage()
      await waitFor(() => expect(getBranding).toHaveBeenCalled())

      fireEvent.click(within(screen.getByRole('group', { name: 'Primary colour presets' })).getByRole('button', { name: 'Red' }))
      fireEvent.click(screen.getByRole('button', { name: 'Save colours' }))

      await waitFor(() => expect(updateBranding).toHaveBeenCalledWith({ primaryColor: '#dc2626', secondaryColor: '' }))
    })

    it('has Arabic names for the swatches', async () => {
      await i18n.changeLanguage('ar')
      try {
        renderPage()
        await waitFor(() => expect(getBranding).toHaveBeenCalled())

        const presets = within(screen.getByRole('group', { name: 'ألوان جاهزة لـ اللون الأساسي' }))
        expect(presets.getByRole('button', { name: 'أزرق' })).toBeInTheDocument()
        expect(presets.getByRole('button', { name: 'وردي' })).toBeInTheDocument()
        expect(screen.getByLabelText('اختيار اللون الأساسي من أداة الألوان')).toBeInTheDocument()
      } finally {
        await i18n.changeLanguage('en')
      }
    })
  })
})
