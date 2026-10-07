import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  createWebhook,
  deleteWebhook,
  listWebhookDeliveries,
  listWebhooks,
  setWebhookEnabled,
  type Webhook,
} from '@/api/integrations'
import { createQueryClient } from '@/app/query-client'
import { WebhooksPage } from './WebhooksPage'

vi.mock('@/api/integrations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/integrations')>()),
  listWebhooks: vi.fn(),
  createWebhook: vi.fn(),
  setWebhookEnabled: vi.fn(),
  deleteWebhook: vi.fn(),
  listWebhookDeliveries: vi.fn(),
}))

const hook: Webhook = {
  id: 'w1',
  name: 'Billing sync',
  url: 'https://example.test/hook',
  events: ['ticket.created'],
  isEnabled: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}

function renderPage() {
  return render(
    <MemoryRouter>
      <QueryClientProvider client={createQueryClient()}>
        <WebhooksPage />
      </QueryClientProvider>
    </MemoryRouter>,
  )
}

describe('WebhooksPage', () => {
  beforeEach(() => {
    vi.mocked(listWebhooks).mockReset().mockResolvedValue([hook])
    vi.mocked(createWebhook).mockReset()
    vi.mocked(setWebhookEnabled).mockReset().mockResolvedValue({ ...hook, isEnabled: false })
    vi.mocked(deleteWebhook).mockReset().mockResolvedValue(undefined)
    vi.mocked(listWebhookDeliveries).mockReset().mockResolvedValue([])
  })

  it('registers a webhook for the chosen events and shows the secret once', async () => {
    vi.mocked(createWebhook).mockResolvedValue({ ...hook, id: 'w2', secret: 'whsec_abc' })
    renderPage()
    await screen.findByText('Billing sync')

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Shop' } })
    fireEvent.change(screen.getByLabelText('URL'), { target: { value: 'https://shop.test/in' } })
    fireEvent.click(screen.getByLabelText(/Ticket resolved/))
    fireEvent.click(screen.getByRole('button', { name: 'Register webhook' }))

    expect(await screen.findByDisplayValue('whsec_abc')).toBeInTheDocument()
    expect(createWebhook).toHaveBeenCalledWith({ name: 'Shop', url: 'https://shop.test/in', events: ['ticket.resolved'] })
    fireEvent.click(screen.getByRole('button', { name: 'I have copied the secret' }))
    expect(screen.queryByDisplayValue('whsec_abc')).not.toBeInTheDocument()
  })

  it('validates the form before calling the API', async () => {
    renderPage()
    await screen.findByText('Billing sync')

    fireEvent.change(screen.getByLabelText('URL'), { target: { value: 'ftp://nope' } })
    fireEvent.click(screen.getByRole('button', { name: 'Register webhook' }))

    expect(await screen.findByText('Enter a name.')).toBeInTheDocument()
    expect(screen.getByText('Enter a valid http or https URL.')).toBeInTheDocument()
    expect(screen.getByText('Choose at least one event.')).toBeInTheDocument()
    expect(createWebhook).not.toHaveBeenCalled()
  })

  it('disables a webhook', async () => {
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Disable Billing sync' }))

    await waitFor(() => expect(setWebhookEnabled).toHaveBeenCalledWith('w1', false))
  })

  it('shows the delivery log of a webhook', async () => {
    vi.mocked(listWebhookDeliveries).mockResolvedValue([
      {
        id: 'd1',
        event: 'ticket.created',
        status: 'failed',
        attempts: 6,
        nextAttemptAt: '2026-10-02T08:00:00Z',
        lastStatusCode: 503,
        lastError: 'The receiver answered 503',
        createdAt: '2026-10-02T07:00:00Z',
        deliveredAt: null,
      },
    ])
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Deliveries of Billing sync' }))

    expect(await screen.findByText('Failed')).toBeInTheDocument()
    expect(screen.getByText('503 The receiver answered 503')).toBeInTheDocument()
    expect(listWebhookDeliveries).toHaveBeenCalledWith('w1', expect.anything())
  })

  it('deletes a webhook', async () => {
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Delete Billing sync' }))

    await waitFor(() => expect(deleteWebhook).toHaveBeenCalledWith('w1'))
  })
})
