import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'
import { ADMIN_PASSWORD, fakeApi, inOneHour } from './test/fake-api'

const ARABIC_NAVIGATION_LABELS = ['لوحة التحكم', 'التذاكر', 'العملاء', 'المهام', 'قاعدة المعرفة', 'التقارير', 'المستخدمون', 'الإسناد', 'فئات التذاكر', 'سياسة SLA']

function html() {
  return document.documentElement
}

async function renderLoginPage() {
  vi.stubGlobal('fetch', fakeApi())
  render(<App />)
  await screen.findByRole('form', { name: 'Sign in' })
}

describe('Language switching (ar / en)', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('starts in English with <html dir="ltr" lang="en">', async () => {
    await renderLoginPage()

    expect(html()).toHaveAttribute('lang', 'en')
    expect(html()).toHaveAttribute('dir', 'ltr')
  })

  it('switching to Arabic sets <html dir="rtl" lang="ar"> and translates the page', async () => {
    await renderLoginPage()

    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))

    expect(await screen.findByRole('form', { name: 'تسجيل الدخول' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'نظام إدارة دعم العملاء' })).toBeInTheDocument()
    expect(html()).toHaveAttribute('lang', 'ar')
    expect(html()).toHaveAttribute('dir', 'rtl')
  })

  it('switching back to English sets <html dir="ltr" lang="en">', async () => {
    await renderLoginPage()
    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))
    await screen.findByRole('form', { name: 'تسجيل الدخول' })

    fireEvent.click(screen.getByRole('button', { name: 'English' }))

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(html()).toHaveAttribute('lang', 'en')
    expect(html()).toHaveAttribute('dir', 'ltr')
  })

  it('shows no English text on the Arabic login page, field errors included', async () => {
    await renderLoginPage()
    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))
    await screen.findByRole('form', { name: 'تسجيل الدخول' })

    fireEvent.click(screen.getByRole('button', { name: 'تسجيل الدخول' }))

    expect(await screen.findByText('أدخل بريدك الإلكتروني.')).toBeInTheDocument()
    expect(screen.getByText('أدخل كلمة المرور.')).toBeInTheDocument()
    // The only Latin word left is the switch back to English.
    expect(document.body.textContent?.match(/[A-Za-z]+/g)).toEqual(['English'])
  })

  it('remembers the chosen language for the next visit', async () => {
    await renderLoginPage()

    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))
    await screen.findByRole('form', { name: 'تسجيل الدخول' })

    expect(localStorage.getItem('crm.language')).toBe('ar')
  })

  it('signed in, Arabic: sidebar on the right, navigation and header in Arabic', async () => {
    saveSession('good-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)
    await screen.findByRole('heading', { level: 1, name: 'Dashboard' })

    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'لوحة التحكم' })).toBeInTheDocument()
    const navigation = screen.getByRole('navigation', { name: 'التنقل الرئيسي' })
    expect(within(navigation).getAllByRole('link').map((link) => link.textContent)).toEqual(ARABIC_NAVIGATION_LABELS)
    expect(screen.getByRole('button', { name: 'تسجيل الخروج' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'إظهار القائمة الجانبية أو إخفاؤها' })).toBeInTheDocument()
    // shadcn Sidebar: no role exposes its side, so the data attribute is the observable result.
    expect(document.querySelector('[data-slot="sidebar"][data-side]')).toHaveAttribute('data-side', 'right')
  })

  it('signs in in Arabic and shows API error toasts in Arabic', async () => {
    vi.stubGlobal('fetch', fakeApi({ healthStatus: 500 }))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'العربية' }))
    await screen.findByRole('form', { name: 'تسجيل الدخول' })

    fireEvent.change(screen.getByLabelText('البريد الإلكتروني'), { target: { value: 'admin@crm.local' } })
    fireEvent.change(screen.getByLabelText('كلمة المرور'), { target: { value: ADMIN_PASSWORD } })
    fireEvent.click(screen.getByRole('button', { name: 'تسجيل الدخول' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'لوحة التحكم' })).toBeInTheDocument()
    expect(await screen.findByText('حدث خطأ ما. حاول مرة أخرى.')).toBeInTheDocument()
    expect(screen.getByText('المرجع: health-1')).toBeInTheDocument()
  })
})
