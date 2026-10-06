import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import {
  createTicketCategory,
  listTicketCategories,
  updateTicketCategory,
  type TicketCategory,
} from '@/api/ticket-categories'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { TicketCategoriesPage } from './TicketCategoriesPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/ticket-categories', () => ({
  listTicketCategories: vi.fn(),
  createTicketCategory: vi.fn(),
  updateTicketCategory: vi.fn(),
}))

const signedInAdmin: CurrentUser = {
  id: '4',
  email: 'admin2@crm.local',
  fullName: 'Admin User',
  roles: ['Admin'],
  permissions: [permissions.ticketsView, permissions.ticketsManage, permissions.categoriesManage],
}
/** Sees tickets but may not change categories (the route is guarded too; the page still hides the actions). */
const signedInAgent: CurrentUser = { ...signedInAdmin, id: '2', roles: ['Agent'], permissions: [permissions.ticketsView] }

const billing: TicketCategory = {
  id: 'k1',
  name: 'Billing',
  isActive: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}
const legacy: TicketCategory = { ...billing, id: 'k2', name: 'Legacy', isActive: false }

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <TicketCategoriesPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function rowOf(name: string) {
  return screen.getByRole('row', { name: new RegExp(name) })
}

describe('TicketCategoriesPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAdmin)
    vi.mocked(listTicketCategories).mockReset().mockResolvedValue([billing, legacy])
    vi.mocked(createTicketCategory).mockReset()
    vi.mocked(updateTicketCategory).mockReset()
  })

  it('lists every category with its status', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Ticket categories' })).toBeInTheDocument()
    expect(await within(await screen.findByRole('row', { name: /Billing/ })).findByText('Active')).toBeInTheDocument()
    expect(within(rowOf('Legacy')).getByText('Inactive')).toBeInTheDocument()
    expect(listTicketCategories).toHaveBeenCalledWith({}, expect.anything())
  })

  it('says so when there is no category yet', async () => {
    vi.mocked(listTicketCategories).mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('No categories yet.')).toBeInTheDocument()
  })

  it('adds a category and reloads the list', async () => {
    vi.mocked(createTicketCategory).mockResolvedValue({ ...billing, id: 'k3', name: 'Delivery' })
    renderPage()
    await screen.findByRole('row', { name: /Billing/ })

    fireEvent.click(await screen.findByRole('button', { name: 'Add category' }))
    const dialog = await screen.findByRole('dialog', { name: 'New category' })
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: '  Delivery ' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(createTicketCategory).toHaveBeenCalledWith({ name: 'Delivery', isActive: true })
    expect(await screen.findByText('Category Delivery was created.')).toBeInTheDocument()
    expect(listTicketCategories).toHaveBeenCalledTimes(2)
  })

  it('requires a name before calling the API', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Add category' }))
    const dialog = await screen.findByRole('dialog', { name: 'New category' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Enter the category name.')).toBeInTheDocument()
    expect(createTicketCategory).not.toHaveBeenCalled()
  })

  it('shows the server message when the name already exists', async () => {
    vi.mocked(createTicketCategory).mockRejectedValue(
      new ApiError('POST /api/ticket-categories failed with status 400', 400, {
        status: 400,
        errors: { name: ['A category with this name already exists.'] },
      }),
    )
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Add category' }))
    const dialog = await screen.findByRole('dialog', { name: 'New category' })
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: 'billing' } })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('A category with this name already exists.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
  })

  it('deactivates a category from the edit dialog', async () => {
    vi.mocked(updateTicketCategory).mockResolvedValue({ ...billing, isActive: false })
    renderPage()
    await screen.findByRole('row', { name: /Billing/ })

    fireEvent.click(await within(rowOf('Billing')).findByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit category' })
    expect(within(dialog).getByLabelText('Name')).toHaveValue('Billing')
    const active = within(dialog).getByRole('checkbox', { name: 'Active (can be chosen for new tickets)' })
    expect(active).toBeChecked()
    fireEvent.click(active)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(updateTicketCategory).toHaveBeenCalledWith('k1', { name: 'Billing', isActive: false }))
    expect(await screen.findByText('Category Billing was saved.')).toBeInTheDocument()
  })

  it('hides add and edit from a user without categories.manage', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInAgent)
    renderPage()
    await screen.findByRole('row', { name: /Billing/ })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(screen.queryByRole('button', { name: 'Add category' })).not.toBeInTheDocument()
    expect(within(rowOf('Billing')).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
  })
})
