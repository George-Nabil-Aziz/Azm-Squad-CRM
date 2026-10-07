import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { WebFormsPage } from './WebFormsPage'

describe('Web forms admin page', () => {
  it('shows the embed snippet that points at the contact form page', () => {
    render(<WebFormsPage />)

    const snippet = screen.getByLabelText('Embed code')
    expect((snippet as HTMLTextAreaElement).value).toContain(`${window.location.origin}/embed/contact`)
    expect((snippet as HTMLTextAreaElement).value).toContain('<iframe')
  })

  it('offers a link that opens the form in a new tab', () => {
    render(<WebFormsPage />)

    const link = screen.getByRole('link', { name: 'Open in new tab' })
    expect(link).toHaveAttribute('href', '/embed/contact')
    expect(link).toHaveAttribute('target', '_blank')
  })

  it('shows a live preview of the form in an iframe', () => {
    render(<WebFormsPage />)

    const preview = screen.getByTitle('Live preview of the contact form')
    expect(preview.tagName).toBe('IFRAME')
    expect(preview).toHaveAttribute('src', '/embed/contact')
  })
})
