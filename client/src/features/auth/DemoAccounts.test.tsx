import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '@/App'
import { clearPortalSession } from '@/auth/portal-session'

function json(status: number, body: unknown, type = 'application/json') {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': type },
  })
}

const DEMO = [
  { email: 'superadmin@crm.com', role: 'SuperAdmin' },
  { email: 'admin@crm.com', role: 'Admin' },
  { email: 'supervisor@crm.com', role: 'Supervisor' },
  { email: 'agent@crm.com', role: 'Agent' },
  { email: 'customer@crm.com', role: 'Customer' },
]

function stubApi(demoStatus: number) {
  vi.stubGlobal('fetch', (path: string) => {
    if (path === '/api/auth/demo-accounts')
      return Promise.resolve(demoStatus === 200 ? json(200, DEMO) : json(demoStatus, { status: demoStatus }, 'application/problem+json'))
    if (path === '/api/branding') return Promise.resolve(json(200, { primaryColor: null, secondaryColor: null, logoUrl: null }))
    if (path === '/api/portal/chatbot/status') return Promise.resolve(json(200, { enabled: false }))
    return Promise.resolve(json(404, { status: 404 }, 'application/problem+json'))
  })
}

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

describe('Demo accounts on the sign-in pages', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    clearPortalSession()
    localStorage.clear()
  })

  it('lists the staff demo accounts with role labels; picking one fills the email and focuses the password', async () => {
    stubApi(200)
    renderAt('/login')

    const section = await screen.findByRole('region', {
      name: 'Demo accounts',
    })
    expect(section).toHaveTextContent('superadmin@crm.com')
    expect(section).toHaveTextContent('Support agent')
    expect(section).not.toHaveTextContent('customer@crm.com')
    fireEvent.click(screen.getByRole('button', { name: /agent@crm.com/ }))

    expect(screen.getByLabelText('Email')).toHaveValue('agent@crm.com')
    await waitFor(() => expect(screen.getByLabelText('Password')).toHaveFocus())
    expect(screen.getByLabelText('Password')).toHaveValue('')
  })

  it('fills the email and the password when the server sends the dev password, so Sign in is one click away', async () => {
    vi.stubGlobal('fetch', (path: string) => {
      if (path === '/api/auth/demo-accounts')
        return Promise.resolve(json(200, DEMO.map((a) => ({ ...a, password: a.role === 'Customer' ? null : 'Dev#Pass1' }))))
      if (path === '/api/branding') return Promise.resolve(json(200, { primaryColor: null, secondaryColor: null, logoUrl: null }))
      return Promise.resolve(json(404, { status: 404 }, 'application/problem+json'))
    })
    renderAt('/login')

    fireEvent.click(await screen.findByRole('button', { name: /supervisor@crm.com/ }))

    expect(screen.getByLabelText('Email')).toHaveValue('supervisor@crm.com')
    expect(screen.getByLabelText('Password')).toHaveValue('Dev#Pass1')
  })

  it('shows the Arabic role labels in Arabic', async () => {
    stubApi(200)
    renderAt('/login')
    fireEvent.click(await screen.findByRole('button', { name: 'العربية' }))

    const section = await screen.findByRole('region', {
      name: 'حسابات تجريبية',
    })
    expect(section).toHaveTextContent('مدير النظام')
    expect(section).toHaveTextContent('مسؤول')
    expect(section).toHaveTextContent('مشرف فريق')
    expect(section).toHaveTextContent('موظف دعم')
  })

  it('shows nothing when the endpoint answers 404', async () => {
    stubApi(404)
    renderAt('/login')
    await screen.findByRole('form', { name: 'Sign in' })

    expect(screen.queryByRole('region', { name: 'Demo accounts' })).not.toBeInTheDocument()
  })

  it('fills the email of the portal sign-in with the demo customer', async () => {
    stubApi(200)
    renderAt('/portal/login')

    const section = await screen.findByRole('region', {
      name: 'Demo accounts',
    })
    expect(section).not.toHaveTextContent('agent@crm.com')
    fireEvent.click(screen.getByRole('button', { name: /customer@crm.com/ }))

    expect(screen.getByLabelText('Email')).toHaveValue('customer@crm.com')
  })

  it('shows nothing on the portal sign-in when the endpoint fails', async () => {
    stubApi(500)
    renderAt('/portal/login')
    await screen.findByRole('form', { name: 'Enter your email' }).catch(() => undefined)

    expect(screen.queryByRole('region', { name: 'Demo accounts' })).not.toBeInTheDocument()
  })
})
