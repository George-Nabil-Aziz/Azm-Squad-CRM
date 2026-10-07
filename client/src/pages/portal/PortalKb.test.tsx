import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '@/App'
import { clearPortalSession } from '@/auth/portal-session'
import { clearSession } from '@/auth/session'
import { setLanguage } from '@/i18n/i18n'

function json(status: number, body: unknown, type = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': type } })
}

let counts = { helpfulCount: 3, notHelpfulCount: 1 }
const headersSeen: Record<string, string>[] = []
const posts: unknown[] = []

function stubApi() {
  const fetchMock = vi.fn(async (path: string, init?: RequestInit) => {
    headersSeen.push((init?.headers ?? {}) as Record<string, string>)
    const arabic = ((init?.headers ?? {}) as Record<string, string>)['Accept-Language'] === 'ar'
    if (path === '/api/portal/kb/faqs')
      return json(200, [{ id: 'f1', question: arabic ? 'كيف أدفع؟' : 'How do I pay?', answer: arabic ? 'عبر الإنترنت.' : 'Online.' }])
    if (path === '/api/portal/kb/categories') return json(200, [{ id: 'c1', name: 'Billing', articleCount: 1 }])
    if (path.startsWith('/api/portal/kb/articles?'))
      return json(200, { items: [{ id: 'a1', title: 'Reset your password', summary: 'Use the link.', categoryName: 'Billing' }], totalCount: 1 })
    if (path.startsWith('/api/portal/kb/search?'))
      return json(200, [{ type: 'article', id: 'a1', title: 'Reset your password', snippet: 'Use the link.', score: 30 }])
    if (path === '/api/portal/kb/articles/a1/feedback') {
      posts.push(JSON.parse(String(init?.body)))
      counts = { helpfulCount: 4, notHelpfulCount: 1 }
      return json(200, counts)
    }
    if (path === '/api/portal/kb/articles/a1')
      return json(200, { id: 'a1', title: 'Reset your password', body: 'Use the reset link.', categoryName: 'Billing', ...counts, publishedAt: null })
    return json(404, { status: 404 }, 'application/problem+json')
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

describe('Portal help center', () => {
  beforeEach(() => {
    counts = { helpfulCount: 3, notHelpfulCount: 1 }
    headersSeen.length = 0
    posts.length = 0
    clearSession()
    clearPortalSession()
    localStorage.clear()
  })

  afterEach(async () => {
    vi.unstubAllGlobals()
    await setLanguage('en')
  })

  it('the logo and name in the portal header lead back to the landing page', async () => {
    renderAt('/portal')

    const banner = await screen.findByRole('banner')
    const home = within(banner).getAllByRole('link')[0]
    expect(home).toHaveTextContent('Support portal')
    expect(home).toHaveAttribute('href', '/')
  })

  it('shows FAQs and published articles without signing in (no Authorization header)', async () => {
    stubApi()
    renderAt('/portal')

    expect(await screen.findByText('How do I pay?')).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Reset your password' })).toHaveAttribute('href', '/portal/kb/articles/a1')
    expect(screen.getByRole('button', { name: 'Billing (1)' })).toBeInTheDocument()
    expect(headersSeen.every((headers) => headers.Authorization === undefined)).toBe(true)
  })

  it('searches the help center', async () => {
    const fetchMock = stubApi()
    renderAt('/portal')

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search help articles and FAQs' }), { target: { value: 'reset' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    const results = await screen.findByRole('region', { name: 'Search results' })
    expect(await within(results).findByRole('link', { name: 'Reset your password' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([path]) => path === '/api/portal/kb/search?q=reset')).toBe(true)
  })

  it('filters the articles by category', async () => {
    const fetchMock = stubApi()
    renderAt('/portal')
    fireEvent.click(await screen.findByRole('button', { name: 'Billing (1)' }))

    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([path]) => String(path).includes('/api/portal/kb/articles?categoryId=c1'))).toBe(true),
    )
  })

  it('reloads the content in the other language when the language is switched', async () => {
    stubApi()
    renderAt('/portal')
    await screen.findByText('How do I pay?')

    fireEvent.click(screen.getByRole('button', { name: 'العربية' }))

    expect(await screen.findByText('كيف أدفع؟')).toBeInTheDocument()
    expect(headersSeen.some((headers) => headers['Accept-Language'] === 'ar')).toBe(true)
  })

  it('shows an article with its counters and records "Was this helpful?" once', async () => {
    stubApi()
    renderAt('/portal/kb/articles/a1')

    expect(await screen.findByRole('heading', { level: 1, name: 'Reset your password' })).toBeInTheDocument()
    expect(screen.getByText('Use the reset link.')).toBeInTheDocument()
    expect(screen.getByText('3 found this helpful, 1 did not.')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Yes' }))

    expect(await screen.findByText('Thank you for your feedback.')).toBeInTheDocument()
    expect(posts).toEqual([{ helpful: true }])
    expect(await screen.findByText('4 found this helpful, 1 did not.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yes' })).not.toBeInTheDocument()
  })

  it('remembers an earlier vote of this browser', async () => {
    localStorage.setItem('crm.portal-kb-vote.a1', 'no')
    stubApi()
    renderAt('/portal/kb/articles/a1')

    expect(await screen.findByText('Thank you for your feedback.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yes' })).not.toBeInTheDocument()
  })

  it('says so when the article is not found', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json(404, { status: 404 }, 'application/problem+json')))
    renderAt('/portal/kb/articles/zzz')

    expect(await screen.findByText('This article was not found.')).toBeInTheDocument()
  })
})
