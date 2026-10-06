import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/api/errors'
import { getSettings, updateSettings, type SystemSettings } from '@/api/settings'
import { createQueryClient } from '@/app/query-client'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { SettingsPage } from './SettingsPage'

vi.mock('@/api/settings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/settings')>()),
  getSettings: vi.fn(),
  updateSettings: vi.fn(),
}))

const settings: SystemSettings = {
  businessHours: { enabled: false, days: ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday'], start: '08:00', end: '16:00' },
  timeZone: 'Asia/Riyadh',
  ticketPrefix: 'TKT-',
  email: {
    fromAddress: 'support@azm.example',
    fromName: null,
    smtpHost: 'smtp.azm.example',
    smtpPort: 587,
    smtpSecurity: 'StartTls',
    smtpUserName: null,
    imapHost: null,
    imapPort: 993,
    imapSecurity: 'SslOnConnect',
    imapUserName: null,
    imapFolder: 'INBOX',
  },
  whatsApp: { phoneNumberId: '555' },
  secretsSet: { smtpPassword: true, imapPassword: false, whatsAppAccessToken: false, whatsAppAppSecret: false, whatsAppVerifyToken: false },
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <SettingsPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

async function loaded() {
  renderPage()
  return screen.findByLabelText('Ticket number prefix')
}

function save() {
  fireEvent.click(screen.getByRole('button', { name: 'Save settings' }))
}

describe('SettingsPage', () => {
  beforeEach(() => {
    vi.mocked(getSettings).mockReset().mockResolvedValue(settings)
    vi.mocked(updateSettings).mockReset().mockResolvedValue(settings)
  })

  it('shows the stored settings', async () => {
    const prefix = await loaded()

    expect(screen.getByRole('heading', { level: 1, name: 'Settings' })).toBeInTheDocument()
    expect(prefix).toHaveValue('TKT-')
    expect(screen.getByLabelText('Time zone')).toHaveValue('Asia/Riyadh')
    expect(screen.getByRole('checkbox', { name: 'Count SLA time only during business hours' })).not.toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Sunday' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Friday' })).not.toBeChecked()
    expect(screen.getByLabelText('SMTP server')).toHaveValue('smtp.azm.example')
    expect(screen.getByLabelText('Phone number ID')).toHaveValue('555')
  })

  it('saves business hours, time zone and prefix', async () => {
    await loaded()

    fireEvent.click(screen.getByRole('checkbox', { name: 'Count SLA time only during business hours' }))
    fireEvent.change(screen.getByLabelText('Opening time'), { target: { value: '09:00' } })
    fireEvent.change(screen.getByLabelText('Ticket number prefix'), { target: { value: 'SUP-' } })
    save()

    await waitFor(() => expect(updateSettings).toHaveBeenCalledTimes(1))
    expect(vi.mocked(updateSettings).mock.calls[0][0]).toMatchObject({
      businessHours: { enabled: true, start: '09:00', end: '16:00', days: ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday'] },
      timeZone: 'Asia/Riyadh',
      ticketPrefix: 'SUP-',
    })
    expect(await screen.findByText('Settings were saved.')).toBeInTheDocument()
  })

  it('never shows a saved secret, and sends none when left empty', async () => {
    await loaded()

    const smtpPassword = screen.getByLabelText('SMTP password')
    expect(smtpPassword).toHaveValue('')
    expect(screen.getAllByText('A value is saved. It is never shown.')).toHaveLength(1)
    expect(screen.getAllByText('No value is saved.')).toHaveLength(4)
    save()

    await waitFor(() => expect(updateSettings).toHaveBeenCalledTimes(1))
    expect(vi.mocked(updateSettings).mock.calls[0][0]).not.toHaveProperty('secrets')
  })

  it('sends a typed secret, and an empty string to remove a saved one', async () => {
    await loaded()

    fireEvent.change(screen.getByLabelText('WhatsApp access token'), { target: { value: 'new-token' } })
    fireEvent.click(screen.getByRole('checkbox', { name: 'Remove the saved SMTP password' }))
    save()

    await waitFor(() => expect(updateSettings).toHaveBeenCalledTimes(1))
    expect(vi.mocked(updateSettings).mock.calls[0][0].secrets).toEqual({ smtpPassword: '', whatsAppAccessToken: 'new-token' })
  })

  it('checks values before calling the API', async () => {
    await loaded()

    fireEvent.change(screen.getByLabelText('Ticket number prefix'), { target: { value: 'A B' } })
    fireEvent.change(screen.getByLabelText('SMTP port'), { target: { value: '70000' } })
    save()

    expect(await screen.findByText('Use 1 to 10 letters or digits, optionally ending with a dash.')).toBeInTheDocument()
    expect(screen.getByText('Enter a port from 1 to 65535.')).toBeInTheDocument()
    expect(updateSettings).not.toHaveBeenCalled()
  })

  it('needs a working day when business hours are on', async () => {
    await loaded()

    fireEvent.click(screen.getByRole('checkbox', { name: 'Count SLA time only during business hours' }))
    for (const day of ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday']) {
      fireEvent.click(screen.getByRole('checkbox', { name: day }))
    }
    save()

    expect(await screen.findByText('Select at least one working day.')).toBeInTheDocument()
    expect(updateSettings).not.toHaveBeenCalled()
  })

  it('shows the server message under the field on 400', async () => {
    vi.mocked(updateSettings).mockRejectedValue(
      new ApiError('PUT /api/settings failed with status 400', 400, {
        status: 400,
        errors: { timeZone: ['This time zone is not known.'] },
      }),
    )
    await loaded()

    fireEvent.change(screen.getByLabelText('Time zone'), { target: { value: 'Mars/Olympus' } })
    save()

    expect(await screen.findByText('This time zone is not known.')).toBeInTheDocument()
    expect(screen.getByLabelText('Time zone')).toHaveAttribute('aria-invalid', 'true')
  })
})
