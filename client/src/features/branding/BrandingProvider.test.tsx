import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getBranding } from '@/api/branding'
import { createQueryClient } from '@/app/query-client'
import { BrandLogo } from './BrandLogo'
import { applyBrandColors } from './branding-context'
import { BrandingProvider } from './BrandingProvider'
import { contrastForeground, DARK_FOREGROUND, LIGHT_FOREGROUND, parseHexColor } from './branding-colors'

vi.mock('@/api/branding', () => ({ getBranding: vi.fn() }))

function renderProvider() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <BrandingProvider>
        <BrandLogo alt="Company" />
      </BrandingProvider>
    </QueryClientProvider>,
  )
}

describe('branding colours', () => {
  it('parses 3 and 6 digit colours and refuses anything else', () => {
    expect(parseHexColor('#fff')).toEqual([255, 255, 255])
    expect(parseHexColor('#0A5CAD')).toEqual([10, 92, 173])
    expect(parseHexColor('blue')).toBeNull()
  })

  it('picks dark text on light colours and light text on dark colours', () => {
    expect(contrastForeground('#ffeb3b')).toBe(DARK_FOREGROUND)
    expect(contrastForeground('#0a2540')).toBe(LIGHT_FOREGROUND)
    expect(contrastForeground('nonsense')).toBe(LIGHT_FOREGROUND)
  })
})

describe('BrandingProvider', () => {
  beforeEach(() => {
    document.documentElement.removeAttribute('style')
    vi.mocked(getBranding).mockReset()
  })

  it('applies the brand colours to the theme variables without a rebuild, and shows the logo', async () => {
    vi.mocked(getBranding).mockResolvedValue({ primaryColor: '#0a2540', secondaryColor: '#ffeb3b', logoUrl: '/api/branding/logo?v=1' })
    renderProvider()

    const root = document.documentElement
    await waitFor(() => expect(root.style.getPropertyValue('--primary')).toBe('#0a2540'))
    expect(root.style.getPropertyValue('--primary-foreground')).toBe(LIGHT_FOREGROUND)
    expect(root.style.getPropertyValue('--secondary')).toBe('#ffeb3b')
    expect(root.style.getPropertyValue('--secondary-foreground')).toBe(DARK_FOREGROUND)
    expect(await screen.findByRole('img', { name: 'Company' })).toHaveAttribute('src', '/api/branding/logo?v=1')
  })

  it('leaves the default theme and shows no logo when nothing is set', async () => {
    vi.mocked(getBranding).mockResolvedValue({ primaryColor: null, secondaryColor: null, logoUrl: null })
    renderProvider()

    await waitFor(() => expect(getBranding).toHaveBeenCalled())
    expect(document.documentElement.style.getPropertyValue('--primary')).toBe('')
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('keeps the default theme when the branding cannot be read', async () => {
    vi.mocked(getBranding).mockRejectedValue(new Error('network'))
    renderProvider()

    await waitFor(() => expect(getBranding).toHaveBeenCalled())
    expect(document.documentElement.style.getPropertyValue('--primary')).toBe('')
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })
})

describe('brand colours in dark mode', () => {
  it('lightens a dark brand primary, keeps readable text and leaves the secondary to the dark palette', () => {
    const root = document.createElement('div')
    applyBrandColors(root, { primaryColor: '#0a2540', secondaryColor: '#ffeb3b' }, 'dark')

    expect(root.style.getPropertyValue('--primary')).toContain('color-mix')
    expect(root.style.getPropertyValue('--primary-foreground')).toBe(DARK_FOREGROUND)
    expect(root.style.getPropertyValue('--secondary')).toBe('')
    expect(root.style.getPropertyValue('--secondary-foreground')).toBe('')
  })

  it('keeps a bright brand primary as it is', () => {
    const root = document.createElement('div')
    applyBrandColors(root, { primaryColor: '#ffeb3b', secondaryColor: null }, 'dark')

    expect(root.style.getPropertyValue('--primary')).toBe('#ffeb3b')
    expect(root.style.getPropertyValue('--primary-foreground')).toBe(DARK_FOREGROUND)
  })
})
