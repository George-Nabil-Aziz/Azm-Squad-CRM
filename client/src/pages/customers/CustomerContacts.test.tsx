import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import {
  addCustomerContact,
  getCustomer,
  listCustomers,
  makeCustomerContactPrimary,
  removeCustomerContact,
  type Customer,
} from '@/api/customers'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { CustomersPage } from './CustomersPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/customers', () => ({
  listCustomers: vi.fn(),
  getCustomer: vi.fn(),
  createCustomer: vi.fn(),
  updateCustomer: vi.fn(),
  deleteCustomer: vi.fn(),
  addCustomerContact: vi.fn(),
  makeCustomerContactPrimary: vi.fn(),
  removeCustomerContact: vi.fn(),
}))

/** An agent: may view and manage customers. */
const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage, permissions.ticketsView, permissions.ticketsManage],
}
/** A user who may only look at customers: sees the contacts, cannot change them. */
const signedInViewer: CurrentUser = { ...signedInAgent, id: '7', permissions: [permissions.customersView] }

const nour: Customer = {
  id: 'c1',
  name: 'Nour Trading',
  email: 'info@nour.example',
  phone: '+966501234567',
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
  contacts: [
    { id: 'k1', type: 'phone', value: '+966501234567', isPrimary: true },
    { id: 'k2', type: 'phone', value: '+966551234567', isPrimary: false },
    { id: 'k3', type: 'email', value: 'info@nour.example', isPrimary: true },
    { id: 'k4', type: 'whatsapp', value: '+966561234567', isPrimary: true },
  ],
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <CustomersPage />
      </MemoryRouter>
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

/** Opens the contacts dialog of Nour Trading from the customers table. */
async function openContacts() {
  renderPage()
  const row = await screen.findByRole('row', { name: /Nour Trading/ })
  fireEvent.click(within(row).getByRole('button', { name: 'Contacts' }))
  const dialog = await screen.findByRole('dialog', { name: 'Contacts of Nour Trading' })
  await within(dialog).findByText('+966551234567')
  return dialog
}

function contactRow(dialog: HTMLElement, value: string) {
  return within(dialog).getByRole('row', { name: new RegExp(value.replace('+', '\\+')) })
}

function fillContactForm(dialog: HTMLElement, values: { type?: string; value?: string }) {
  if (values.type !== undefined) fireEvent.change(within(dialog).getByLabelText('Type'), { target: { value: values.type } })
  if (values.value !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Phone number or email'), { target: { value: values.value } })
}

describe('Customer contacts', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(listCustomers).mockReset().mockResolvedValue({ items: [nour], page: 1, pageSize: 20, totalCount: 1 })
    vi.mocked(getCustomer).mockReset().mockResolvedValue(nour)
    vi.mocked(addCustomerContact).mockReset()
    vi.mocked(makeCustomerContactPrimary).mockReset().mockResolvedValue(undefined)
    vi.mocked(removeCustomerContact).mockReset().mockResolvedValue(undefined)
  })

  it('lists every phone, email and WhatsApp number with the primary ones marked', async () => {
    const dialog = await openContacts()

    expect(getCustomer).toHaveBeenCalledWith('c1', expect.anything())
    expect(within(contactRow(dialog, '+966501234567')).getByText('Phone')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966501234567')).getByText('Primary')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966551234567')).queryByText('Primary')).not.toBeInTheDocument()
    expect(within(contactRow(dialog, 'info@nour.example')).getByText('Email')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966561234567')).getByText('WhatsApp')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966561234567')).getByText('Primary')).toBeInTheDocument()
  })

  it('adds a phone number and reloads the contacts', async () => {
    vi.mocked(addCustomerContact).mockResolvedValue({ id: 'k5', type: 'phone', value: '+966571234567', isPrimary: false })
    const dialog = await openContacts()

    fillContactForm(dialog, { value: ' 057 123 4567 ' })
    fireEvent.click(await within(dialog).findByRole('button', { name: 'Add contact' }))

    await waitFor(() =>
      expect(addCustomerContact).toHaveBeenCalledWith('c1', { type: 'phone', value: '057 123 4567', isPrimary: false }),
    )
    expect(await screen.findByText('Contact added.')).toBeInTheDocument()
    await waitFor(() => expect(getCustomer).toHaveBeenCalledTimes(2))
    expect(within(dialog).getByLabelText('Phone number or email')).toHaveValue('')
  })

  it('adds an email as the primary email', async () => {
    vi.mocked(addCustomerContact).mockResolvedValue({ id: 'k5', type: 'email', value: 'sales@nour.example', isPrimary: true })
    const dialog = await openContacts()

    fillContactForm(dialog, { type: 'email', value: 'sales@nour.example' })
    fireEvent.click(await within(dialog).findByRole('checkbox', { name: 'Make it the primary contact of its type' }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Add contact' }))

    await waitFor(() =>
      expect(addCustomerContact).toHaveBeenCalledWith('c1', { type: 'email', value: 'sales@nour.example', isPrimary: true }),
    )
  })

  it('checks the value before calling the API', async () => {
    const dialog = await openContacts()
    const add = await within(dialog).findByRole('button', { name: 'Add contact' })

    fireEvent.click(add)
    expect(await within(dialog).findByText('Enter a phone number or an email address.')).toBeInTheDocument()

    fillContactForm(dialog, { value: 'call me' })
    fireEvent.click(add)
    expect(await within(dialog).findByText('Enter a valid phone number, e.g. +966501234567 or 0501234567.')).toBeInTheDocument()

    fillContactForm(dialog, { type: 'email', value: 'not-an-email' })
    fireEvent.click(add)
    expect(await within(dialog).findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Phone number or email')).toHaveAttribute('aria-invalid', 'true')
    expect(addCustomerContact).not.toHaveBeenCalled()
  })

  it('shows the server message next to the value when the API answers 400', async () => {
    vi.mocked(addCustomerContact).mockRejectedValue(
      new ApiError('POST /api/customers/c1/contacts failed with status 400', 400, {
        status: 400,
        errors: { value: ['That number does not exist.'] },
      }),
    )
    const dialog = await openContacts()

    fillContactForm(dialog, { value: '0521234567' })
    fireEvent.click(await within(dialog).findByRole('button', { name: 'Add contact' }))

    expect(await within(dialog).findByText('That number does not exist.')).toBeInTheDocument()
  })

  it('shows the conflict message next to the value when the customer already has the contact', async () => {
    vi.mocked(addCustomerContact).mockRejectedValue(
      new ApiError('POST /api/customers/c1/contacts failed with status 409', 409, {
        status: 409,
        detail: 'The customer already has this contact.',
      }),
    )
    const dialog = await openContacts()

    fillContactForm(dialog, { value: '0501234567' })
    fireEvent.click(await within(dialog).findByRole('button', { name: 'Add contact' }))

    expect(await within(dialog).findByText('The customer already has this contact.')).toBeInTheDocument()
  })

  it('makes another phone the primary one', async () => {
    const dialog = await openContacts()

    fireEvent.click(await within(contactRow(dialog, '+966551234567')).findByRole('button', { name: 'Make primary' }))

    await waitFor(() => expect(makeCustomerContactPrimary).toHaveBeenCalledWith('c1', 'k2'))
    expect(await screen.findByText('Primary contact changed.')).toBeInTheDocument()
    expect(within(contactRow(dialog, '+966501234567')).queryByRole('button', { name: 'Make primary' })).not.toBeInTheDocument()
  })

  it('removes a contact', async () => {
    const dialog = await openContacts()

    fireEvent.click(await within(contactRow(dialog, '+966551234567')).findByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(removeCustomerContact).toHaveBeenCalledWith('c1', 'k2'))
    expect(await screen.findByText('Contact removed.')).toBeInTheDocument()
  })

  it('shows the contacts read-only to a user who may only view customers', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInViewer)
    const dialog = await openContacts()
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(within(contactRow(dialog, '+966551234567')).getByText('Phone')).toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Add contact' })).not.toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Make primary' })).not.toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument()
  })
})
