import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useLayoutEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  createQuickReply,
  deleteQuickReply,
  listQuickReplies,
  updateQuickReply,
  type QuickReply,
} from '@/api/quick-replies'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import { Can } from '@/features/auth/Can'
import { useCurrentUser } from '@/features/auth/useCurrentUser'
import { QuickReplyBody, QuickReplyPreview } from '@/features/quick-replies/QuickReplyBody'
import { insertAt, quickReplyTokens } from '@/features/quick-replies/placeholders'

const quickRepliesQueryKey = ['quick-replies'] as const

/** Saved replies (CRM-32): personal ones and the shared ones; shared ones are only changed with quick-replies.manage-shared. */
export function QuickRepliesPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const replies = useQuery({ queryKey: quickRepliesQueryKey, queryFn: ({ signal }) => listQuickReplies('', signal) })
  const currentUser = useCurrentUser()
  const bodyRef = useRef<HTMLTextAreaElement>(null)
  const pendingCaret = useRef<number | null>(null)
  const [filter, setFilter] = useState('')
  const [editing, setEditing] = useState<QuickReply | null>(null)
  const [title, setTitle] = useState('')
  const [shortcut, setShortcut] = useState('')
  const [body, setBody] = useState('')
  const [shared, setShared] = useState(false)
  const [errors, setErrors] = useState<{ title?: string; body?: string }>({})
  const visible = useMemo(() => {
    const needle = filter.trim().toLowerCase()
    if (!needle) return replies.data
    return replies.data?.filter((r) => [r.title, r.shortcut ?? '', r.body].some((value) => value.toLowerCase().includes(needle)))
  }, [replies.data, filter])
  const reload = () => queryClient.invalidateQueries({ queryKey: quickRepliesQueryKey })

  const save = useMutation({
    mutationFn: (request: { title: string; shortcut: string | null; body: string; isShared: boolean }) =>
      editing ? updateQuickReply(editing.id, request) : createQuickReply(request),
    onSuccess: reload,
  })
  const remove = useMutation({ mutationFn: (id: string) => deleteQuickReply(id), onSuccess: reload })

  // After a chip inserts a token, put the caret behind it and keep the focus in the text box.
  useLayoutEffect(() => {
    if (pendingCaret.current === null) return
    bodyRef.current?.focus()
    bodyRef.current?.setSelectionRange(pendingCaret.current, pendingCaret.current)
    pendingCaret.current = null
  }, [body])

  function insertToken(token: string) {
    const box = bodyRef.current
    const result = insertAt(body, token, box?.selectionStart ?? body.length, box?.selectionEnd ?? body.length)
    pendingCaret.current = result.caret
    setBody(result.text)
  }

  function reset() {
    setEditing(null)
    setTitle('')
    setShortcut('')
    setBody('')
    setShared(false)
    setErrors({})
  }

  function edit(reply: QuickReply) {
    setEditing(reply)
    setTitle(reply.title)
    setShortcut(reply.shortcut ?? '')
    setBody(reply.body)
    setShared(reply.isShared)
    setErrors({})
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    const next = {
      title: title.trim() ? undefined : t('quickReplies.titleRequired'),
      body: body.trim() ? undefined : t('quickReplies.bodyRequired'),
    }
    setErrors(next)
    if (next.title || next.body) return
    try {
      await save.mutateAsync({ title: title.trim(), shortcut: shortcut.trim() || null, body: body.trim(), isShared: shared })
      reset()
    } catch {
      // The API client already shows the error as a toast (403 for a shared reply without permission).
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-primary">{t('nav.quickReplies')}</h1>
        <p className="text-muted-foreground">{t('quickReplies.description')}</p>
      </div>

      <form onSubmit={(event) => void submit(event)} noValidate className="flex max-w-xl flex-col gap-3 rounded-xl border bg-card p-4">
        <h2 className="text-lg font-medium">{t(editing ? 'quickReplies.formTitleEdit' : 'quickReplies.formTitleNew')}</h2>
        <div className="flex flex-col gap-1">
          <label htmlFor="quick-reply-title" className="text-sm font-medium">
            {t('quickReplies.title')}
          </label>
          <Input id="quick-reply-title" value={title} aria-invalid={errors.title ? true : undefined} onChange={(e) => setTitle(e.target.value)} />
          {errors.title ? <p role="alert" className="text-sm text-destructive">{errors.title}</p> : null}
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="quick-reply-shortcut" className="text-sm font-medium">
            {t('quickReplies.shortcut')}
          </label>
          <Input id="quick-reply-shortcut" dir="ltr" value={shortcut} onChange={(e) => setShortcut(e.target.value)} />
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="quick-reply-body" className="text-sm font-medium">
            {t('quickReplies.body')}
          </label>
          <Textarea ref={bodyRef} id="quick-reply-body" dir="auto" rows={4} value={body} aria-invalid={errors.body ? true : undefined} onChange={(e) => setBody(e.target.value)} />
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="text-xs text-muted-foreground">{t('quickReplies.placeholders')}</span>
            {quickReplyTokens.map(({ token, key }) => (
              <Button
                key={key}
                type="button"
                variant="outline"
                size="sm"
                className="h-6 rounded-full px-2 text-xs"
                aria-label={t('quickReplies.insert', { label: t(`quickReplies.tokens.${key}`) })}
                onClick={() => insertToken(token)}
              >
                + {t(`quickReplies.tokens.${key}`)}
              </Button>
            ))}
          </div>
          {errors.body ? <p role="alert" className="text-sm text-destructive">{errors.body}</p> : null}
        </div>
        {body.trim() ? (
          <div className="flex flex-col gap-1">
            <span className="text-sm font-medium">{t('quickReplies.preview')}</span>
            <QuickReplyPreview body={body} agentName={currentUser.data?.fullName} />
            <p className="text-xs text-muted-foreground">{t('quickReplies.previewHint')}</p>
          </div>
        ) : null}
        <Can permission={permissions.quickRepliesManageShared}>
          <div className="flex items-center gap-2">
            <Checkbox id="quick-reply-shared" checked={shared} onCheckedChange={(checked) => setShared(checked === true)} />
            <label htmlFor="quick-reply-shared" className="text-sm">
              {t('quickReplies.share')}
            </label>
          </div>
        </Can>
        <div className="flex gap-2">
          <Button type="submit" disabled={save.isPending}>
            {t('quickReplies.save')}
          </Button>
          {editing ? (
            <Button type="button" variant="outline" onClick={reset}>
              {t('quickReplies.cancel')}
            </Button>
          ) : null}
        </div>
      </form>

      {replies.isPending ? <p className="text-muted-foreground">{t('quickReplies.loading')}</p> : null}
      {replies.data?.length === 0 ? (
        <div className="flex flex-col items-center gap-1 rounded-xl border border-dashed p-10 text-center">
          <p className="font-medium">{t('quickReplies.emptyTitle')}</p>
          <p className="text-sm text-muted-foreground">{t('quickReplies.emptyHint')}</p>
        </div>
      ) : null}
      {replies.data && replies.data.length > 0 ? (
        <div className="flex flex-col gap-3">
          <div className="flex max-w-sm flex-col gap-1">
            <label htmlFor="quick-reply-filter" className="text-sm font-medium">
              {t('quickReplies.filter')}
            </label>
            <Input id="quick-reply-filter" value={filter} onChange={(e) => setFilter(e.target.value)} />
          </div>
          {visible?.length === 0 ? <p className="text-muted-foreground">{t('quickReplies.noMatches')}</p> : null}
          {visible && visible.length > 0 ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('quickReplies.title')}</TableHead>
                  <TableHead className="hidden md:table-cell">{t('quickReplies.preview')}</TableHead>
                  <TableHead>{t('quickReplies.shortcut')}</TableHead>
                  <TableHead>{t('quickReplies.visibility')}</TableHead>
                  <TableHead className="text-end">{t('quickReplies.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {visible.map((reply) => (
                  <TableRow key={reply.id}>
                    <TableCell className="max-w-48 font-medium">
                      <div className="truncate" title={reply.title}>
                        {reply.title}
                      </div>
                    </TableCell>
                    <TableCell className="hidden max-w-sm md:table-cell">
                      <QuickReplyBody body={reply.body} className="block truncate text-sm text-muted-foreground" />
                    </TableCell>
                    <TableCell dir="ltr" className="text-start">
                      {reply.shortcut ? <code className="rounded bg-muted px-1.5 py-0.5 text-xs">{reply.shortcut}</code> : null}
                    </TableCell>
                    <TableCell>
                      <Badge variant={reply.isShared ? 'secondary' : 'outline'}>
                        {t(reply.isShared ? 'quickReplies.shared' : 'quickReplies.personal')}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-2">
                        <Button variant="outline" size="sm" onClick={() => edit(reply)}>
                          {t('quickReplies.edit')}
                        </Button>
                        <Button variant="outline" size="sm" disabled={remove.isPending} onClick={() => remove.mutate(reply.id)}>
                          {t('quickReplies.delete')}
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}
