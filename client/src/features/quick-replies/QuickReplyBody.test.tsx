import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { i18n } from '@/i18n/i18n'
import { QuickReplyBody, QuickReplyPreview } from './QuickReplyBody'

describe('QuickReplyBody', () => {
  it('shows placeholders as badges with friendly labels, not raw tokens', () => {
    render(<QuickReplyBody body="Hello {{customer.name}}, ticket {{ticket.number}} ({{ ticket.subject }}) - {{agent.name}}" />)

    for (const label of ['Customer name', 'Ticket number', 'Ticket subject', 'Agent name']) {
      expect(screen.getByText(label)).toBeInTheDocument()
    }
    expect(screen.queryByText(/\{\{/)).not.toBeInTheDocument()
    expect(screen.getByText(/Hello/)).toBeInTheDocument()
  })

  it('shows Arabic labels', async () => {
    await i18n.changeLanguage('ar')
    render(<QuickReplyBody body="{{customer.name}}" />)

    expect(screen.getByText('اسم العميل')).toBeInTheDocument()
  })
})

describe('QuickReplyPreview', () => {
  it('substitutes sample values', () => {
    render(<QuickReplyPreview body="Hi {{customer.name}}, {{ticket.number}} {{ticket.subject}} - {{agent.name}}" agentName="Sara Agent" />)

    expect(screen.getByTestId('quick-reply-preview')).toHaveTextContent('Hi Ahmed Ali, TKT-000123 Invoice issue - Sara Agent')
  })

  it('uses localised samples in Arabic', async () => {
    await i18n.changeLanguage('ar')
    render(<QuickReplyPreview body="{{customer.name}}" agentName="سارة" />)

    expect(screen.getByTestId('quick-reply-preview')).toHaveTextContent('أحمد علي')
  })
})
