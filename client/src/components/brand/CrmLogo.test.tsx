import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import '@/i18n/i18n'
import { CrmLogo } from './CrmLogo'

describe('CrmLogo', () => {
  it('draws the mark with the brand primary colour and a readable name', () => {
    render(<CrmLogo alt="Company logo" />)
    expect(screen.getByRole('img', { name: 'Company logo' })).toHaveClass('text-primary')
  })

  it('is decorative without an alt text, and the full variant adds the product name', () => {
    render(<CrmLogo variant="full" />)
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
    expect(screen.getByText('Customer Support CRM')).toBeInTheDocument()
  })
})
