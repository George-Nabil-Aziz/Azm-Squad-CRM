import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '@/App'
import { ADMIN_PASSWORD, fakeApi, submitSignIn } from './test/fake-api'

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

describe('Login page', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('shows the logo and product name as a link to the landing page, and still signs in', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/login')

    const home = await screen.findByRole('link', { name: 'Customer Support CRM' })
    expect(home).toHaveAttribute('href', '/')
    expect(home.querySelector('svg')).not.toBeNull()
    expect(screen.getByText('Every customer conversation, one place.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /language|العربية/i })).toBeInTheDocument()

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)
    expect(await screen.findByText('System Administrator')).toBeInTheDocument()
  })

  it('opens the landing page from the logo link', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/login')

    fireEvent.click(await screen.findByRole('link', { name: 'Customer Support CRM' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Customer support, all in one place' })).toBeInTheDocument()
  })
})
