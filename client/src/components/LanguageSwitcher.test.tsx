import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { i18n } from '@/i18n/i18n'
import { LanguageSwitcher } from './LanguageSwitcher'

describe('LanguageSwitcher', () => {
  it('is labelled with the other language and switches the language on click', async () => {
    render(<LanguageSwitcher />)
    const toArabic = screen.getByRole('button', { name: 'العربية' })

    fireEvent.click(toArabic)
    await screen.findByRole('button', { name: 'English' })
    expect(i18n.resolvedLanguage).toBe('ar')
  })
})
