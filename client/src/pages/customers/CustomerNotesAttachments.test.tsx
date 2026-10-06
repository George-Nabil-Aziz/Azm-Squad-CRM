import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import {
  addCustomerNote,
  downloadCustomerAttachment,
  getCustomer,
  getCustomerTimeline,
  listCustomerAttachments,
  listCustomerNotes,
  uploadCustomerAttachment,
  type Customer,
  type CustomerAttachment,
  type CustomerNote,
} from '@/api/customers'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { saveFile } from '@/lib/save-file'
import { CustomerDetailsPage } from './CustomerDetailsPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/customers', () => ({
  getCustomer: vi.fn(),
  getCustomerTimeline: vi.fn(),
  listCustomerNotes: vi.fn(),
  addCustomerNote: vi.fn(),
  listCustomerAttachments: vi.fn(),
  uploadCustomerAttachment: vi.fn(),
  downloadCustomerAttachment: vi.fn(),
  makeCustomerContactPrimary: vi.fn(),
  removeCustomerContact: vi.fn(),
}))

vi.mock('@/lib/save-file', () => ({ saveFile: vi.fn() }))

const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage],
}
const signedInViewer: CurrentUser = { ...signedInAgent, id: '7', permissions: [permissions.customersView] }

const nour: Customer = {
  id: 'c1',
  name: 'Nour Trading',
  email: null,
  phone: null,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
  contacts: [],
}

const firstNote: CustomerNote = {
  id: 'n1',
  text: 'Prefers WhatsApp.',
  authorId: 'u2',
  authorName: 'Sara Agent',
  createdAt: '2026-10-02T09:30:00Z',
}

const report: CustomerAttachment = {
  id: 'a1',
  fileName: 'report.pdf',
  contentType: 'application/pdf',
  size: 2048,
  uploadedById: 'u2',
  uploadedByName: 'Sara Agent',
  uploadedAt: '2026-10-02T10:00:00Z',
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={['/customers/c1']}>
        <Routes>
          <Route path="/customers/:id" element={<CustomerDetailsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function pdf(name = 'contract.pdf', size?: number) {
  const file = new File(['%PDF-1.7'], name, { type: 'application/pdf' })
  if (size !== undefined) Object.defineProperty(file, 'size', { value: size })
  return file
}

async function chooseFile(file: File) {
  const input = await screen.findByLabelText('File')
  fireEvent.change(input, { target: { files: [file] } })
}

describe('Customer notes and attachments', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(getCustomer).mockReset().mockResolvedValue(nour)
    vi.mocked(getCustomerTimeline).mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0 })
    vi.mocked(listCustomerNotes).mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0 })
    vi.mocked(addCustomerNote).mockReset()
    vi.mocked(listCustomerAttachments).mockReset().mockResolvedValue([])
    vi.mocked(uploadCustomerAttachment).mockReset()
    vi.mocked(downloadCustomerAttachment).mockReset()
    vi.mocked(saveFile).mockReset()
  })

  it('adds a note and shows it with author and time', async () => {
    vi.mocked(addCustomerNote).mockImplementation(async () => {
      vi.mocked(listCustomerNotes).mockResolvedValue({ items: [firstNote], page: 1, pageSize: 10, totalCount: 1 })
      return firstNote
    })
    renderPage()

    fireEvent.change(await screen.findByLabelText('New note'), { target: { value: 'Prefers WhatsApp.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add note' }))

    const notes = await screen.findByRole('list', { name: 'Notes' })
    const [note] = await within(notes).findAllByRole('listitem')
    expect(note).toHaveTextContent('Prefers WhatsApp.')
    expect(note).toHaveTextContent('Sara Agent')
    expect(within(note).getByText((_, element) => element?.tagName === 'TIME')).toHaveAttribute(
      'datetime',
      '2026-10-02T09:30:00Z',
    )
    expect(addCustomerNote).toHaveBeenCalledWith('c1', 'Prefers WhatsApp.')
    expect(screen.getByLabelText('New note')).toHaveValue('')
  })

  it('does not send an empty note', async () => {
    renderPage()

    fireEvent.change(await screen.findByLabelText('New note'), { target: { value: '   ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add note' }))

    expect(await screen.findByText('Write the note first.')).toBeInTheDocument()
    expect(addCustomerNote).not.toHaveBeenCalled()
  })

  it('uploads an allowed file and lists it', async () => {
    vi.mocked(uploadCustomerAttachment).mockImplementation(async () => {
      vi.mocked(listCustomerAttachments).mockResolvedValue([report])
      return report
    })
    renderPage()
    const file = pdf('report.pdf')

    await chooseFile(file)
    fireEvent.click(screen.getByRole('button', { name: 'Upload' }))

    const row = await screen.findByRole('row', { name: /report\.pdf/ })
    expect(row).toHaveTextContent('Sara Agent')
    expect(row).toHaveTextContent('2 KB')
    expect(uploadCustomerAttachment).toHaveBeenCalledWith('c1', file)
  })

  it('downloads an attachment', async () => {
    vi.mocked(listCustomerAttachments).mockResolvedValue([report])
    const blob = new Blob(['%PDF'])
    vi.mocked(downloadCustomerAttachment).mockResolvedValue(blob)
    renderPage()

    const row = await screen.findByRole('row', { name: /report\.pdf/ })
    fireEvent.click(within(row).getByRole('button', { name: 'Download' }))

    await waitFor(() => expect(saveFile).toHaveBeenCalledWith(blob, 'report.pdf'))
    expect(downloadCustomerAttachment).toHaveBeenCalledWith('c1', 'a1')
  })

  it('rejects a file over 10 MB before uploading', async () => {
    renderPage()

    await chooseFile(pdf('big.pdf', 10 * 1024 * 1024 + 1))
    fireEvent.click(screen.getByRole('button', { name: 'Upload' }))

    expect(await screen.findByText('The file is larger than 10 MB.')).toBeInTheDocument()
    expect(uploadCustomerAttachment).not.toHaveBeenCalled()
  })

  it('rejects a disallowed file type before uploading', async () => {
    renderPage()

    await chooseFile(new File(['MZ'], 'setup.exe', { type: 'application/x-msdownload' }))
    fireEvent.click(screen.getByRole('button', { name: 'Upload' }))

    expect(await screen.findByText(/This file type is not allowed/)).toBeInTheDocument()
    expect(uploadCustomerAttachment).not.toHaveBeenCalled()
  })

  it('shows the server message when the upload is refused', async () => {
    vi.mocked(uploadCustomerAttachment).mockRejectedValue(
      new ApiError('POST failed with status 400', 400, { status: 400, errors: { file: ['The file is empty.'] } }),
    )
    renderPage()

    await chooseFile(pdf('empty.pdf'))
    fireEvent.click(screen.getByRole('button', { name: 'Upload' }))

    expect(await screen.findByText('The file is empty.')).toBeInTheDocument()
  })

  it('hides the note and upload forms from a user who may only view', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInViewer)
    vi.mocked(listCustomerAttachments).mockResolvedValue([report])
    renderPage()

    const row = await screen.findByRole('row', { name: /report\.pdf/ })

    expect(within(row).getByRole('button', { name: 'Download' })).toBeInTheDocument()
    expect(screen.queryByLabelText('New note')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('File')).not.toBeInTheDocument()
  })
})
