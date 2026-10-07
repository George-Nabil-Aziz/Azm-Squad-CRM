import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/query-client'
import { TicketReplyForm } from './TicketReplyForm'

function stubApi() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } })),
  )
}

function renderForm(channel: 'sms' | 'email') {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <TicketReplyForm ticketId="t1" channel={channel} />
    </QueryClientProvider>,
  )
}

describe('Reply box of an SMS ticket', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('warns when the text needs more than one SMS segment', () => {
    stubApi()
    renderForm('sms')

    fireEvent.change(screen.getByLabelText('Message'), { target: { value: 'a'.repeat(161) } })

    expect(screen.getByRole('alert')).toHaveTextContent('2 SMS segments')
  })

  it('does not warn for a short text', () => {
    stubApi()
    renderForm('sms')

    fireEvent.change(screen.getByLabelText('Message'), { target: { value: 'Your order ships today.' } })

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('does not warn on an email ticket', () => {
    stubApi()
    renderForm('email')

    fireEvent.change(screen.getByLabelText('Message'), { target: { value: 'a'.repeat(500) } })

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
