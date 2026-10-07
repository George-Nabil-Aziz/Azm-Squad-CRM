import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createApiKey, listApiKeys, revokeApiKey, type ApiKey } from '@/api/integrations'
import { createQueryClient } from '@/app/query-client'
import { ApiKeysPage } from './ApiKeysPage'

vi.mock('@/api/integrations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/integrations')>()),
  listApiKeys: vi.fn(),
  createApiKey: vi.fn(),
  revokeApiKey: vi.fn(),
}))

const existing: ApiKey = {
  id: 'k1',
  name: 'Shop connector',
  keyPrefix: 'crm_Ab3x',
  scopes: ['tickets:read'],
  createdAt: '2026-10-01T08:00:00Z',
  lastUsedAt: null,
  revokedAt: null,
}

function renderPage() {
  return render(
    <MemoryRouter>
      <QueryClientProvider client={createQueryClient()}>
        <ApiKeysPage />
      </QueryClientProvider>
    </MemoryRouter>,
  )
}

describe('ApiKeysPage', () => {
  beforeEach(() => {
    vi.mocked(listApiKeys).mockReset().mockResolvedValue([existing])
    vi.mocked(createApiKey).mockReset()
    vi.mocked(revokeApiKey).mockReset().mockResolvedValue(undefined)
  })

  it('lists the keys with their prefix, never the key', async () => {
    renderPage()

    expect(await screen.findByText('Shop connector')).toBeInTheDocument()
    expect(screen.getByText('crm_Ab3x…')).toBeInTheDocument()
    expect(screen.getAllByText('Read tickets')).toHaveLength(2) // the scope checkbox and the row
  })

  it('creates a key with the chosen scopes and shows the new key once', async () => {
    vi.mocked(createApiKey).mockResolvedValue({ ...existing, id: 'k2', name: 'ERP', key: 'crm_secret-value' })
    renderPage()
    await screen.findByText('Shop connector')

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'ERP' } })
    fireEvent.click(screen.getByLabelText(/Create tickets/))
    fireEvent.click(screen.getByRole('button', { name: 'Create API key' }))

    expect(await screen.findByDisplayValue('crm_secret-value')).toBeInTheDocument()
    expect(createApiKey).toHaveBeenCalledWith({ name: 'ERP', scopes: ['tickets:write'] })

    fireEvent.click(screen.getByRole('button', { name: 'I have copied the key' }))
    expect(screen.queryByDisplayValue('crm_secret-value')).not.toBeInTheDocument()
  })

  it('asks for a name and a scope before calling the API', async () => {
    renderPage()
    await screen.findByText('Shop connector')

    fireEvent.click(screen.getByRole('button', { name: 'Create API key' }))

    expect(await screen.findByText('Enter a name.')).toBeInTheDocument()
    expect(screen.getByText('Choose at least one scope.')).toBeInTheDocument()
    expect(createApiKey).not.toHaveBeenCalled()
  })

  it('revokes a key', async () => {
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Revoke Shop connector' }))

    await waitFor(() => expect(revokeApiKey).toHaveBeenCalledWith('k1'))
  })
})
