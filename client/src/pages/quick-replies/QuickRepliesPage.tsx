import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
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

const quickRepliesQueryKey = ['quick-replies'] as const

/** Saved replies (CRM-32): personal ones and the shared ones; shared ones are only changed with quick-replies.manage-shared. */
export function QuickRepliesPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const replies = useQuery({ queryKey: quickRepliesQueryKey, queryFn: ({ signal }) => listQuickReplies('', signal) })
  const [editing, setEditing] = useState<QuickReply | null>(null)
  const [title, setTitle] = useState('')
  const [shortcut, setShortcut] = useState('')
  const [body, setBody] = useState('')
  const [shared, setShared] = useState(false)
  const [errors, setErrors] = useState<{ title?: string; body?: string }>({})
  const reload = () => queryClient.invalidateQueries({ queryKey: quickRepliesQueryKey })

  const save = useMutation({
    mutationFn: (request: { title: string; shortcut: string | null; body: string; isShared: boolean }) =>
      editing ? updateQuickReply(editing.id, request) : createQuickReply(request),
    onSuccess: reload,
  })
  const remove = useMutation({ mutationFn: (id: string) => deleteQuickReply(id), onSuccess: reload })

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
        <h1 className="text-2xl font-semibold">{t('nav.quickReplies')}</h1>
        <p className="text-muted-foreground">{t('quickReplies.description')}</p>
      </div>

      <form onSubmit={(event) => void submit(event)} noValidate className="flex max-w-xl flex-col gap-3">
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
          <Textarea id="quick-reply-body" dir="auto" rows={4} value={body} aria-invalid={errors.body ? true : undefined} onChange={(e) => setBody(e.target.value)} />
          <p className="text-xs text-muted-foreground">{t('quickReplies.placeholders')}</p>
          {errors.body ? <p role="alert" className="text-sm text-destructive">{errors.body}</p> : null}
        </div>
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
      {replies.data?.length === 0 ? <p className="text-muted-foreground">{t('quickReplies.empty')}</p> : null}
      {replies.data && replies.data.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('quickReplies.title')}</TableHead>
              <TableHead>{t('quickReplies.shortcut')}</TableHead>
              <TableHead>{t('quickReplies.visibility')}</TableHead>
              <TableHead className="text-end">{t('quickReplies.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {replies.data.map((reply) => (
              <TableRow key={reply.id}>
                <TableCell className="font-medium">{reply.title}</TableCell>
                <TableCell dir="ltr" className="text-start">
                  {reply.shortcut}
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
  )
}
