import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { callsTo, fakeApi, submitSignIn } from './test/fake-api'

describe('Login page', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the sign-in form with the app name when signed out', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Customer Support CRM' })).toBeInTheDocument()
  })

  it('shows an inline error and no toast for a wrong password', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', 'wrong')

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(screen.queryByText('Something went wrong. Please try again.')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('admin@crm.local')
  })

  it('shows field errors and does not call the API when the fields are empty', async () => {
    const fetchMock = fakeApi()
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)
    await screen.findByRole('form', { name: 'Sign in' })

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Enter your email.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
    expect(callsTo(fetchMock, '/api/auth/login')).toBe(0)
  })

  it('shows a field error for an invalid email address', async () => {
    const fetchMock = fakeApi()
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('not-an-email', 'whatever')

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(callsTo(fetchMock, '/api/auth/login')).toBe(0)
  })
})
