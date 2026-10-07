import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { i18n } from '@/i18n/i18n'
import { LandingPage } from './LandingPage'

function renderPage() {
  return render(
    <MemoryRouter>
      <LandingPage />
    </MemoryRouter>,
  )
}

describe('LandingPage', () => {
  it('has the headline, the two calls to action and the three steps', () => {
    renderPage()
    expect(screen.getByRole('heading', { level: 1, name: 'Customer support, all in one place' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login')
    expect(screen.getByRole('link', { name: 'Customer portal' })).toHaveAttribute('href', '/portal')
    expect(screen.getByRole('heading', { level: 2, name: 'How it works' })).toBeInTheDocument()
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
  })

  it('has the language and theme buttons in the header and a footer', () => {
    renderPage()
    expect(screen.getByRole('button', { name: 'العربية' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Switch to dark mode' })).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toHaveTextContent(`© ${new Date().getFullYear()}`)
  })

  it('is available in Arabic', async () => {
    await i18n.changeLanguage('ar')
    renderPage()
    expect(screen.getByRole('link', { name: 'تسجيل الدخول' })).toHaveAttribute('href', '/login')
    expect(screen.getByRole('link', { name: 'بوابة العملاء' })).toHaveAttribute('href', '/portal')
  })
})
