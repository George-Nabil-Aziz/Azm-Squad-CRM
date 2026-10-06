import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/api/errors'
import { createTask, listTasks, markTaskDone, type WorkTask } from '@/api/tasks'
import { createQueryClient } from '@/app/query-client'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { TasksPage } from './TasksPage'

vi.mock('@/api/tasks', () => ({ listTasks: vi.fn(), createTask: vi.fn(), markTaskDone: vi.fn() }))

const call: WorkTask = {
  id: 'k1',
  title: 'Call Nour',
  description: null,
  dueAt: '2026-10-07T10:00:00Z',
  ticketId: 't1',
  ticketNumber: 'TKT-000001',
  isDone: false,
  completedAt: null,
  createdAt: '2026-10-06T08:00:00Z',
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <TasksPage />
        <ApiErrorToaster />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function future(hours: number) {
  const date = new Date(Date.now() + hours * 3600_000)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

describe('TasksPage', () => {
  beforeEach(() => {
    vi.mocked(listTasks).mockReset().mockResolvedValue([call])
    vi.mocked(createTask).mockReset().mockResolvedValue(call)
    vi.mocked(markTaskDone).mockReset().mockResolvedValue({ ...call, isDone: true })
  })

  it('lists my open tasks with the ticket link', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Tasks' })).toBeInTheDocument()
    const row = await screen.findByRole('row', { name: /Call Nour/ })
    expect(within(row).getByRole('link', { name: 'TKT-000001' })).toHaveAttribute('href', '/tickets/t1')
  })

  it('creates a task with the due date as an ISO time and reloads the list', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Call Nour/ })

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Send quote' } })
    fireEvent.change(screen.getByLabelText('Due date and time'), { target: { value: future(2) } })
    fireEvent.click(screen.getByRole('button', { name: 'Add task' }))

    await waitFor(() => expect(createTask).toHaveBeenCalled())
    const body = vi.mocked(createTask).mock.calls[0][0]
    expect(body.title).toBe('Send quote')
    expect(body.dueAt).toMatch(/Z$/)
    await waitFor(() => expect(listTasks).toHaveBeenCalledTimes(2))
  })

  it('rejects a due date in the past before calling the API', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Call Nour/ })

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Too late' } })
    fireEvent.change(screen.getByLabelText('Due date and time'), { target: { value: future(-2) } })
    fireEvent.click(screen.getByRole('button', { name: 'Add task' }))

    expect(await screen.findByText('Choose a date and time in the future.')).toBeInTheDocument()
    expect(createTask).not.toHaveBeenCalled()
  })

  it('shows the server message on 400', async () => {
    vi.mocked(createTask).mockRejectedValue(
      new ApiError('POST /api/tasks failed with status 400', 400, { status: 400, errors: { dueAt: ['The due date must be in the future.'] } }),
    )
    renderPage()
    await screen.findByRole('row', { name: /Call Nour/ })

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'x' } })
    fireEvent.change(screen.getByLabelText('Due date and time'), { target: { value: future(1) } })
    fireEvent.click(screen.getByRole('button', { name: 'Add task' }))

    expect(await screen.findByText('The due date must be in the future.')).toBeInTheDocument()
  })

  it('marks a task done and reloads the list', async () => {
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Mark as done: Call Nour' }))

    await waitFor(() => expect(markTaskDone).toHaveBeenCalledWith('k1'))
    await waitFor(() => expect(listTasks).toHaveBeenCalledTimes(2))
  })

  it('shows a message when there are no open tasks', async () => {
    vi.mocked(listTasks).mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('You have no open tasks.')).toBeInTheDocument()
  })
})
