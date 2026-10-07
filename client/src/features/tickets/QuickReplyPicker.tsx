import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { listQuickReplies, renderQuickReply } from '@/api/quick-replies'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { QuickReplyBody } from '@/features/quick-replies/QuickReplyBody'

/**
 * The quick reply picker under the reply box (CRM-32): search by title or shortcut, choose one, and its text, with the
 * placeholders replaced by the ticket's data (server side), is handed to `onInsert`.
 */
export function QuickReplyPicker({ ticketId, onInsert }: { ticketId: string; onInsert: (text: string) => void }) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const replies = useQuery({
    queryKey: ['quick-replies', search],
    queryFn: ({ signal }) => listQuickReplies(search, signal),
    enabled: open,
  })
  const insert = useMutation({
    mutationFn: (id: string) => renderQuickReply(id, ticketId),
    onSuccess: ({ text }) => {
      onInsert(text)
      setOpen(false)
    },
  })

  return (
    <div className="flex flex-col gap-2">
      <Button type="button" variant="outline" size="sm" className="self-start" aria-expanded={open} onClick={() => setOpen(!open)}>
        {t('quickReplies.picker')}
      </Button>
      {open ? (
        <div className="flex flex-col gap-2 rounded-lg border p-3">
          <label htmlFor="quick-reply-search" className="text-sm font-medium">
            {t('quickReplies.search')}
          </label>
          <Input id="quick-reply-search" value={search} onChange={(event) => setSearch(event.target.value)} />
          {replies.data?.length === 0 ? <p className="text-sm text-muted-foreground">{t('quickReplies.empty')}</p> : null}
          <ul className="flex flex-col gap-1">
            {replies.data?.map((reply) => (
              <li key={reply.id}>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="h-auto w-full flex-col items-start justify-start gap-0.5 text-start whitespace-normal"
                  disabled={insert.isPending}
                  onClick={() => insert.mutate(reply.id)}
                >
                  <span>
                    <span className="font-medium">{reply.title}</span>
                    {reply.shortcut ? (
                      <span dir="ltr" className="ms-2 text-muted-foreground">
                        {reply.shortcut}
                      </span>
                    ) : null}
                  </span>
                  <QuickReplyBody body={reply.body} className="line-clamp-2 text-xs font-normal leading-5 text-muted-foreground" />
                </Button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  )
}
