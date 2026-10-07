import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { connectChatHub, getChatAvailability, startChat, submitOfflineChat, type ChatConnection, type ChatMessage } from '@/api/chat'
import { isApiError } from '@/api/errors'
import type { WebFormReceipt } from '@/api/web-forms'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ChatPanel } from '@/features/chat/ChatPanel'
import { ContactForm } from '@/features/web-forms/ContactForm'

/**
 * The embeddable live chat widget (CRM-56, `/embed/chat`): asks for name + email and starts a chat when an agent is online,
 * otherwise shows the offline form (which opens a ticket).
 */
export function ChatWidgetPage() {
  const { t } = useTranslation()
  const availability = useQuery({ queryKey: ['chat', 'availability'], queryFn: ({ signal }) => getChatAvailability(signal) })
  const [offline, setOffline] = useState(false)
  const [receipt, setReceipt] = useState<WebFormReceipt | null>(null)
  const [sessionId, setSessionId] = useState<string | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [ended, setEnded] = useState(false)
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [first, setFirst] = useState('')
  const [error, setError] = useState<string | null>(null)
  const connection = useRef<ChatConnection | null>(null)

  useEffect(
    () => () => {
      void connection.current?.stop()
    },
    [],
  )

  async function start(event: FormEvent) {
    event.preventDefault()
    setError(null)
    if (!name.trim() || !email.trim()) {
      setError(t('chat.widget.required'))
      return
    }
    try {
      const started = await startChat({ name: name.trim(), email: email.trim(), message: first.trim() })
      const id = started.session.id
      connection.current = await connectChatHub({
        visitor: { sessionId: id, token: started.visitorToken },
        handlers: {
          onMessage: (message) => setMessages((all) => (all.some((m) => m.id === message.id) ? all : [...all, message])),
          onChatEnded: () => setEnded(true),
        },
      })
      if (first.trim()) {
        setMessages([{ id: `first-${id}`, sessionId: id, sender: 'visitor', senderName: name.trim(), body: first.trim(), sentAt: started.session.startedAt }])
      }
      setSessionId(id)
    } catch (caught) {
      if (isApiError(caught) && caught.status === 409) setOffline(true)
      else if (isApiError(caught) && caught.status === 400) setError(Object.values(caught.problem?.errors ?? {})[0]?.[0] ?? t('chat.widget.failed'))
      else setError(t('chat.widget.failed'))
    }
  }

  if (receipt) {
    return (
      <main className="mx-auto flex max-w-xl flex-col gap-3 p-4">
        <p role="status" className="font-medium">
          {t('chat.widget.offlineSent')}
        </p>
        {receipt.number ? <p dir="ltr">{receipt.number}</p> : null}
      </main>
    )
  }

  return (
    <main className="mx-auto flex w-full max-w-xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">{t('chat.widget.title')}</h1>
      {availability.isPending ? (
        <p className="text-muted-foreground">{t('chat.widget.checking')}</p>
      ) : offline || availability.data?.available === false ? (
        <>
          <p className="text-muted-foreground">{t('chat.widget.offline')}</p>
          <ContactForm submit={submitOfflineChat} onSubmitted={setReceipt} />
        </>
      ) : sessionId ? (
        <ChatPanel
          messages={messages}
          mine="visitor"
          ended={ended}
          onSend={(body) => connection.current!.send(sessionId, body)}
          onEnd={() => void connection.current?.end(sessionId)}
        />
      ) : (
        <form aria-label={t('chat.widget.title')} noValidate onSubmit={start}>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="chat-name">{t('chat.widget.name')}</FieldLabel>
              <Input id="chat-name" value={name} onChange={(event) => setName(event.target.value)} autoComplete="name" dir="auto" />
            </Field>
            <Field>
              <FieldLabel htmlFor="chat-email">{t('chat.widget.email')}</FieldLabel>
              <Input id="chat-email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="email" dir="auto" />
            </Field>
            <Field>
              <FieldLabel htmlFor="chat-first">{t('chat.widget.firstMessage')}</FieldLabel>
              <Input id="chat-first" value={first} onChange={(event) => setFirst(event.target.value)} dir="auto" />
            </Field>
            {error ? <FieldError errors={[{ message: error }]} /> : null}
            <Button type="submit">{t('chat.widget.start')}</Button>
          </FieldGroup>
        </form>
      )}
    </main>
  )
}
