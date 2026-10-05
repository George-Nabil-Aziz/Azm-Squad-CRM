import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/api/errors'
import {
  createUser,
  deactivateUser,
  listUsers,
  reactivateUser,
  updateUser,
  type PagedResult,
  type User,
} from '@/api/users'
import { createQueryClient } from '@/app/query-client'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { UsersPage } from './UsersPage'

vi.mock('@/api/users', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/users')>()),
  listUsers: vi.fn(),
  createUser: vi.fn(),
  updateUser: vi.fn(),
  deactivateUser: vi.fn(),
  reactivateUser: vi.fn(),
}))

const admin: User = { id: '1', email: 'admin@crm.local', fullName: 'System Administrator', roles: ['SuperAdmin'], isActive: true }
const sara: User = { id: '2', email: 'sara@crm.local', fullName: 'Sara Agent', roles: ['Agent', 'Supervisor'], isActive: true }
const omar: User = { id: '3', email: 'omar@crm.local', fullName: 'Omar Former', roles: ['Agent'], isActive: false }

function pageOf(items: User[], totalCount = items.length, page = 1): PagedResult<User> {
  return { items, page, pageSize: 20, totalCount }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <UsersPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function rowOf(name: string) {
  return screen.getByRole('row', { name: new RegExp(name) })
}

function fillUserForm(dialog: HTMLElement, values: { fullName?: string; email?: string; password?: string }) {
  if (values.fullName !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Full name'), { target: { value: values.fullName } })
  if (values.email !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Email'), { target: { value: values.email } })
  if (values.password !== undefined)
    fireEvent.change(within(dialog).getByLabelText('Password'), { target: { value: values.password } })
}

describe('UsersPage', () => {
  beforeEach(() => {
    vi.mocked(listUsers).mockReset().mockResolvedValue(pageOf([admin, sara, omar]))
    vi.mocked(createUser).mockReset()
    vi.mocked(updateUser).mockReset()
    vi.mocked(deactivateUser).mockReset().mockResolvedValue(undefined)
    vi.mocked(reactivateUser).mockReset().mockResolvedValue(undefined)
  })

  it('lists users with email, roles and status', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Users' })).toBeInTheDocument()
    const row = await screen.findByRole('row', { name: /Sara Agent/ })
    expect(within(row).getByText('sara@crm.local')).toBeInTheDocument()
    expect(within(row).getByText('Support agent, Team supervisor')).toBeInTheDocument()
    expect(within(row).getByText('Active')).toBeInTheDocument()
    expect(within(rowOf('Omar Former')).getByText('Inactive')).toBeInTheDocument()
    expect(listUsers).toHaveBeenCalledWith({ search: undefined, page: 1, pageSize: 20 }, expect.anything())
  })

  it('searches by name or email and starts again at page 1', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })
    vi.mocked(listUsers).mockResolvedValue(pageOf([sara]))

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search by name or email' }), { target: { value: ' sara ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() =>
      expect(listUsers).toHaveBeenLastCalledWith({ search: 'sara', page: 1, pageSize: 20 }, expect.anything()),
    )
    await waitFor(() => expect(screen.queryByRole('row', { name: /Omar Former/ })).not.toBeInTheDocument())
  })

  it('shows "No users found." when nothing matches', async () => {
    vi.mocked(listUsers).mockResolvedValue(pageOf([]))
    renderPage()

    expect(await screen.findByText('No users found.')).toBeInTheDocument()
  })

  it('pages through the results', async () => {
    vi.mocked(listUsers).mockResolvedValue(pageOf([admin, sara], 45))
    renderPage()

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
    expect(listUsers).toHaveBeenLastCalledWith({ search: undefined, page: 2, pageSize: 20 }, expect.anything())
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('creates a user and reloads the list', async () => {
    vi.mocked(createUser).mockResolvedValue({ id: '4', email: 'lina@crm.local', fullName: 'Lina New', roles: ['Agent'], isActive: true })
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    const dialog = await screen.findByRole('dialog', { name: 'New user' })
    fillUserForm(dialog, { fullName: 'Lina New', email: 'lina@crm.local', password: 'Agent#Pass1' })
    fireEvent.click(within(dialog).getByRole('checkbox', { name: 'Support agent' }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(createUser).toHaveBeenCalledWith({
      fullName: 'Lina New',
      email: 'lina@crm.local',
      password: 'Agent#Pass1',
      roles: ['Agent'],
    })
    expect(await screen.findByText('User Lina New was created.')).toBeInTheDocument()
    expect(listUsers).toHaveBeenCalledTimes(2)
  })

  it('checks the form before calling the API', async () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    const dialog = await screen.findByRole('dialog', { name: 'New user' })
    fillUserForm(dialog, { email: 'not-an-email', password: 'weak' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Enter the full name.')).toBeInTheDocument()
    expect(within(dialog).getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(
      within(dialog).getByText('Use at least 8 characters with an upper-case letter, a lower-case letter, a digit and a symbol.'),
    ).toBeInTheDocument()
    expect(within(dialog).getByText('Select at least one role.')).toBeInTheDocument()
    expect(createUser).not.toHaveBeenCalled()
  })

  it('shows the server message next to the email when it is already used', async () => {
    vi.mocked(createUser).mockRejectedValue(
      new ApiError('POST /api/users failed with status 400', 400, {
        status: 400,
        errors: { email: ['This email is already used by another user.'] },
      }),
    )
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: 'Add user' }))
    const dialog = await screen.findByRole('dialog', { name: 'New user' })
    fillUserForm(dialog, { fullName: 'Copy', email: 'admin@crm.local', password: 'Agent#Pass1' })
    fireEvent.click(within(dialog).getByRole('checkbox', { name: 'Support agent' }))

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('This email is already used by another user.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
  })

  it('edits a user: the dialog starts with the current values and has no password field', async () => {
    vi.mocked(updateUser).mockResolvedValue({ ...sara, fullName: 'Sara Lead' })
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.click(within(rowOf('Sara Agent')).getByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit user' })
    expect(within(dialog).getByLabelText('Email')).toHaveValue('sara@crm.local')
    expect(within(dialog).queryByLabelText('Password')).not.toBeInTheDocument()
    expect(within(dialog).getByRole('checkbox', { name: 'Team supervisor' })).toBeChecked()
    fillUserForm(dialog, { fullName: 'Sara Lead' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(updateUser).toHaveBeenCalledWith('2', {
        fullName: 'Sara Lead',
        email: 'sara@crm.local',
        roles: ['Agent', 'Supervisor'],
      }),
    )
    expect(await screen.findByText('User Sara Lead was saved.')).toBeInTheDocument()
  })

  it('deactivates a user after confirmation', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.click(within(rowOf('Sara Agent')).getByRole('button', { name: 'Deactivate' }))
    const confirm = await screen.findByRole('alertdialog', { name: 'Deactivate Sara Agent?' })
    expect(deactivateUser).not.toHaveBeenCalled()
    fireEvent.click(within(confirm).getByRole('button', { name: 'Deactivate' }))

    await waitFor(() => expect(deactivateUser).toHaveBeenCalledWith('2'))
    expect(await screen.findByText('Sara Agent was deactivated.')).toBeInTheDocument()
  })

  it('reactivates an inactive user', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Omar Former/ })

    fireEvent.click(within(rowOf('Omar Former')).getByRole('button', { name: 'Reactivate' }))

    await waitFor(() => expect(reactivateUser).toHaveBeenCalledWith('3'))
  })
})
