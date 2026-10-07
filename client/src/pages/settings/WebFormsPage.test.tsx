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

  it('offers a link to preview the form', () => {
    render(<WebFormsPage />)

    expect(screen.getByRole('link', { name: 'Open the form' })).toHaveAttribute('href', '/embed/contact')
  })
})
