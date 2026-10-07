import { useMutation, useQuery } from '@tanstack/react-query'
import { SendIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { getChatbotStatus, sendChatbotMessage, type ChatbotMessage, type ChatbotReply } from '@/api/portal-chatbot'
import { PORTAL_LOGIN_PATH } from '@/app/portal-paths'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

interface Entry extends ChatbotMessage {
  reply?: ChatbotReply
}

/** The help chatbot (CRM-54, route /portal/chat): answers from the knowledge base, cites the article, offers a human when it cannot answer. */
export function PortalChatPage() {
  const { t } = useTranslation()
  const status = useQuery({ queryKey: ['portal-chatbot', 'status'], queryFn: ({ signal }) => getChatbotStatus(signal) })
  const [entries, setEntries] = useState<Entry[]>([])
  const [text, setText] = useState('')
  const [failed, setFailed] = useState(false)

  const send = useMutation({
    mutationFn: ({ messages, handoff }: { messages: Entry[]; handoff: boolean }) =>
      sendChatbotMessage(
        messages.map(({ role, content }) => ({ role, content })),
        handoff,
      ),
    onSuccess: (reply) => {
      setFailed(false)
      setEntries((current) => [...current, { role: 'assistant', content: reply.answer, reply }])
    },
    onError: () => setFailed(true),
  })

  function submit(content: string, handoff: boolean) {
    const messages: Entry[] = [...entries, { role: 'user', content }]
    setEntries(messages)
    send.mutate({ messages, handoff })
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    const content = text.trim()
    if (!content || send.isPending) return
    setText('')
    submit(content, false)
  }

  if (status.isPending) return <p className="text-muted-foreground">{t('portal.chat.loading')}</p>
  if (!status.data?.enabled) return <p className="text-muted-foreground">{t('portal.chat.unavailable')}</p>

  const last = entries.at(-1)?.reply
  const offerAgent = last?.offerAgent === true && !send.isPending

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-semibold text-primary">{t('portal.chat.title')}</h1>
      <p className="text-muted-foreground">{t('portal.chat.intro')}</p>

      <div role="log" aria-label={t('portal.chat.conversation')} className="flex flex-col gap-3">
        {entries.map((entry, index) =>
          entry.role === 'user' ? (
            <p key={index} dir="auto" className="ms-auto max-w-[85%] whitespace-pre-line rounded-lg bg-primary px-3 py-2 text-primary-foreground">
              {entry.content}
            </p>
          ) : (
            <div key={index} className="flex max-w-[85%] flex-col gap-1 rounded-lg border px-3 py-2">
              <p dir={entry.reply?.language === 'ar' ? 'rtl' : 'ltr'} className="whitespace-pre-line">
                {entry.content}
              </p>
              {entry.reply && entry.reply.sources.length > 0 ? (
                <p className="text-sm text-muted-foreground">
                  {t('portal.chat.sources')}{' '}
                  {entry.reply.sources.map((source) => (
                    <Link key={source.id} to={`/portal/kb/articles/${source.id}`} className="text-primary underline-offset-4 hover:underline">
                      {source.title}
                    </Link>
                  ))}
                </p>
              ) : null}
              {entry.reply?.ticket ? (
                <p className="text-sm">
                  {t('portal.chat.ticketCreated')}{' '}
                  <Link to={`/portal/tickets/${entry.reply.ticket.id}`} dir="ltr" className="text-primary underline-offset-4 hover:underline">
                    {entry.reply.ticket.number}
                  </Link>
                </p>
              ) : null}
              {entry.reply?.signInRequired ? (
                <p className="text-sm">
                  <Link to={PORTAL_LOGIN_PATH} className="text-primary underline-offset-4 hover:underline">
                    {t('portal.signIn')}
                  </Link>
                </p>
              ) : null}
            </div>
          ),
        )}
        {send.isPending ? <p className="text-sm text-muted-foreground">{t('portal.chat.thinking')}</p> : null}
        {failed ? (
          <p role="alert" className="text-sm text-destructive">
            {t('portal.chat.failed')}
          </p>
        ) : null}
      </div>

      {offerAgent ? (
        <div>
          <Button type="button" variant="outline" onClick={() => submit(t('portal.chat.agentRequest'), true)}>
            {t('portal.chat.talkToAgent')}
          </Button>
        </div>
      ) : null}

      <form onSubmit={onSubmit} className="flex items-end gap-2">
        <div className="flex flex-1 flex-col gap-1">
          <label htmlFor="portal-chat-message" className="text-sm font-medium">
            {t('portal.chat.message')}
          </label>
          <Input id="portal-chat-message" dir="auto" value={text} onChange={(event) => setText(event.target.value)} autoComplete="off" />
        </div>
        <Button type="submit" disabled={send.isPending}>
          <SendIcon aria-hidden="true" className="rtl:-scale-x-100" />
          {t('portal.chat.send')}
        </Button>
      </form>
    </div>
  )
}
