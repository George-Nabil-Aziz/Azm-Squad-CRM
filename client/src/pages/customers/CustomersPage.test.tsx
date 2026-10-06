import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { createCustomer, deleteCustomer, listCustomers, updateCustomer, type Customer } from '@/api/customers'
import { ApiError } from '@/api/errors'
import type { PagedResult } from '@/api/paging'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { CustomersPage } from './CustomersPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/customers', () => ({
  listCustomers: vi.fn(),
  createCustomer: vi.fn(),
  updateCustomer: vi.fn(),
  deleteCustomer: vi.fn(),
}))

/** An agent: may view and manage customers. */
const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage, permissions.ticketsView, permissions.ticketsManage],
}
/** A user who may only look at customers (no seeded role has this today; the UI must still hide the actions). */
const signedInViewer: CurrentUser = { ...signedInAgent, id: '7', permissions: [permissions.customersView] }

const nour: Customer = {
  id: 'c1',
  name: 'Nour Trading',
  email: 'info@nour.example',
  phone: '+966 50 123 4567',
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}
const omar: Customer = {
  id: 'c2',
  name: 'Omar Walk-in',
  email: null,
  phone: null,
  createdAt: '2026-10-02T08:00:00Z',
  updatedAt: '2026-10-02T08:00:00Z',
}

function pageOf(items: Customer[], totalCount = items.length, page = 1): PagedResult<Customer> {
  return { items, page, pageSize: 20, totalCount }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <CustomersPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function rowOf(name: string) {
  return screen.getByRole('row', { name: new RegExp(name) })
}

function fillCustomerForm(dialog: HTMLElement, values: { name?: string; email?: string; phone?: string }) {
  if (values.name !== undefined) fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: values.name } })
  if (values.email !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Email'), { target: { value: values.email } })
  if (values.phone !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Phone'), { target: { value: values.phone } })
}

describe('CustomersPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(listCustomers).mockReset().mockResolvedValue(pageOf([nour, omar]))
    vi.mocked(createCustomer).mockReset()
    vi.mocked(updateCustomer).mockReset()
    vi.mocked(deleteCustomer).mockReset().mockResolvedValue(undefined)
  })

  it('lists customers with phone and email', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Customers' })).toBeInTheDocument()
    const row = await screen.findByRole('row', { name: /Nour Trading/ })
    expect(within(row).getByText('+966 50 123 4567')).toBeInTheDocument()
    expect(within(row).getByText('info@nour.example')).toBeInTheDocument()
    expect(rowOf('Omar Walk-in')).toBeInTheDocument()
    expect(listCustomers).toHaveBeenCalledWith({ search: undefined, page: 1, pageSize: 20 }, expect.anything())
  })

  it('searches by name, phone or email and starts again at page 1', async () => {
    vi.mocked(listCustomers).mockResolvedValue(pageOf([nour, omar], 45))
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() =>
      expect(listCustomers).toHaveBeenLastCalledWith({ search: undefined, page: 2, pageSize: 20 }, expect.anything()),
    )
    vi.mocked(listCustomers).mockResolvedValue(pageOf([nour]))

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search by name, phone or email' }), {
      target: { value: ' +966 50 ' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() =>
      expect(listCustomers).toHaveBeenLastCalledWith({ search: '+966 50', page: 1, pageSize: 20 }, expect.anything()),
    )
    await waitFor(() => expect(screen.queryByRole('row', { name: /Omar Walk-in/ })).not.toBeInTheDocument())
  })

  it('shows "No customers found." when nothing matches', async () => {
    vi.mocked(listCustomers).mockResolvedValue(pageOf([]))
    renderPage()

    expect(await screen.findByText('No customers found.')).toBeInTheDocument()
  })

  it('pages through the results', async () => {
    vi.mocked(listCustomers).mockResolvedValue(pageOf([nour, omar], 45))
    renderPage()

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
    expect(listCustomers).toHaveBeenLastCalledWith({ search: undefined, page: 2, pageSize: 20 }, expect.anything())
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('creates a customer with only a name and reloads the list', async () => {
    vi.mocked(createCustomer).mockResolvedValue({ ...omar, id: 'c3', name: 'Lina Store' })
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })

    fireEvent.click(await screen.findByRole('button', { name: 'Add customer' }))
    const dialog = await screen.findByRole('dialog', { name: 'New customer' })
    fillCustomerForm(dialog, { name: '  Lina Store  ' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(createCustomer).toHaveBeenCalledWith({ name: 'Lina Store', email: null, phone: null })
    expect(await screen.findByText('Customer Lina Store was created.')).toBeInTheDocument()
    expect(listCustomers).toHaveBeenCalledTimes(2)
  })

  it('requires a name and checks email and phone before calling the API', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Add customer' }))
    const dialog = await screen.findByRole('dialog', { name: 'New customer' })
    fillCustomerForm(dialog, { name: '   ', email: 'not-an-email', phone: 'call me' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Enter the customer name.')).toBeInTheDocument()
    expect(within(dialog).getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(
      within(dialog).getByText('Enter a phone number with at least 6 digits; it may start with + and contain spaces, dashes or brackets.'),
    ).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
    expect(createCustomer).not.toHaveBeenCalled()
  })

  it('shows the server message next to the field when the API answers 400', async () => {
    vi.mocked(createCustomer).mockRejectedValue(
      new ApiError('POST /api/customers failed with status 400', 400, {
        status: 400,
        errors: { name: ["'Name' must not be empty."] },
      }),
    )
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Add customer' }))
    const dialog = await screen.findByRole('dialog', { name: 'New customer' })
    fillCustomerForm(dialog, { name: 'Nour' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText("'Name' must not be empty.")).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
  })

  it('edits a customer: the dialog starts with the current profile', async () => {
    vi.mocked(updateCustomer).mockResolvedValue({ ...nour, name: 'Nour Trading Co.', email: null })
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })

    fireEvent.click(await within(rowOf('Nour Trading')).findByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit customer' })
    expect(within(dialog).getByLabelText('Name')).toHaveValue('Nour Trading')
    expect(within(dialog).getByLabelText('Phone')).toHaveValue('+966 50 123 4567')
    fillCustomerForm(dialog, { name: 'Nour Trading Co.', email: '' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(updateCustomer).toHaveBeenCalledWith('c1', { name: 'Nour Trading Co.', email: null, phone: '+966 50 123 4567' }),
    )
    expect(await screen.findByText('Customer Nour Trading Co. was saved.')).toBeInTheDocument()
  })

  it('deletes a customer after confirmation', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })

    fireEvent.click(await within(rowOf('Nour Trading')).findByRole('button', { name: 'Delete' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Delete Nour Trading?' })
    expect(within(confirm).getByText('The customer disappears from the list. Their tickets are kept.')).toBeInTheDocument()
    expect(deleteCustomer).not.toHaveBeenCalled()
    fireEvent.click(within(confirm).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleteCustomer).toHaveBeenCalledWith('c1'))
    expect(await screen.findByText('Customer Nour Trading was deleted.')).toBeInTheDocument()
    expect(listCustomers).toHaveBeenCalledTimes(2)
  })

  it('hides add, edit and delete from a user who may only view customers', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInViewer)
    renderPage()
    await screen.findByRole('row', { name: /Nour Trading/ })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(screen.queryByRole('button', { name: 'Add customer' })).not.toBeInTheDocument()
    expect(within(rowOf('Nour Trading')).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
    expect(within(rowOf('Nour Trading')).queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument()
  })
})
