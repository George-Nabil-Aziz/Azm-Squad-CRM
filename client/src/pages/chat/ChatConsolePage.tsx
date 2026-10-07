import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { connectChatHub, listChatMessages, listChatSessions, type ChatConnection, type ChatMessage, type ChatSession } from '@/api/chat'
import { Button } from '@/components/ui/button'
import { ChatPanel } from '@/features/chat/ChatPanel'

const queueKey = ['chat', 'waiting'] as const
const mineKey = ['chat', 'active'] as const

/** Agent console (CRM-56): the waiting queue, accept a chat, talk, end it (the transcript becomes a ticket). */
export function ChatConsolePage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const connection = useRef<ChatConnection | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [live, setLive] = useState<ChatMessage[]>([])
  const [endedIds, setEndedIds] = useState<string[]>([])
  const [connectionFailed, setConnectionFailed] = useState(false)

  const waiting = useQuery({ queryKey: queueKey, queryFn: ({ signal }) => listChatSessions('waiting', signal) })
  const mine = useQuery({ queryKey: mineKey, queryFn: ({ signal }) => listChatSessions('active', signal) })
  const history = useQuery({
    queryKey: ['chat', 'messages', selectedId],
    queryFn: ({ signal }) => listChatMessages(selectedId!, signal),
    enabled: selectedId !== null,
  })

  useEffect(() => {
    let closed = false
    connectChatHub({
      handlers: {
        onChatStarted: () => void queryClient.invalidateQueries({ queryKey: queueKey }),
        onChatAccepted: () => {
          void queryClient.invalidateQueries({ queryKey: queueKey })
          void queryClient.invalidateQueries({ queryKey: mineKey })
        },
        onMessage: (message) => setLive((all) => (all.some((m) => m.id === message.id) ? all : [...all, message])),
        onChatEnded: (session) => {
          setEndedIds((ids) => [...ids, session.id])
          void queryClient.invalidateQueries({ queryKey: mineKey })
        },
      },
    })
      .then((opened) => {
        if (closed) void opened.stop()
        else connection.current = opened
      })
      .catch(() => setConnectionFailed(true))
    return () => {
      closed = true
      void connection.current?.stop()
      connection.current = null
    }
  }, [queryClient])

  const selected: ChatSession | undefined = [...(mine.data ?? []), ...(waiting.data ?? [])].find((s) => s.id === selectedId)
  const ended = selectedId !== null && endedIds.includes(selectedId)
  const messages = selectedId
    ? [...(history.data ?? []), ...live.filter((m) => m.sessionId === selectedId && !(history.data ?? []).some((h) => h.id === m.id))]
    : []

  async function accept(session: ChatSession) {
    await connection.current?.accept(session.id)
    await queryClient.invalidateQueries({ queryKey: queueKey })
    await queryClient.invalidateQueries({ queryKey: mineKey })
    setSelectedId(session.id)
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold text-primary">{t('nav.chat')}</h1>
      {connectionFailed ? <p role="alert" className="text-destructive">{t('chat.connectionFailed')}</p> : null}
      <div className="grid gap-6 md:grid-cols-[18rem_1fr]">
        <div className="flex flex-col gap-4">
          <section aria-label={t('chat.queue')} className="flex flex-col gap-2">
            <h2 className="font-medium">{t('chat.queue')}</h2>
            {waiting.data?.length ? (
              <ul className="flex flex-col gap-2">
                {waiting.data.map((session) => (
                  <li key={session.id} className="flex items-center justify-between gap-2 rounded-lg border p-2">
                    <span dir="auto">{session.visitorName}</span>
                    <Button size="sm" onClick={() => void accept(session)}>
                      {t('chat.accept')}
                    </Button>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-muted-foreground">{t('chat.queueEmpty')}</p>
            )}
          </section>
          <section aria-label={t('chat.mine')} className="flex flex-col gap-2">
            <h2 className="font-medium">{t('chat.mine')}</h2>
            <ul className="flex flex-col gap-2">
              {mine.data?.map((session) => (
                <li key={session.id}>
                  <Button variant={session.id === selectedId ? 'secondary' : 'outline'} className="w-full justify-start" onClick={() => setSelectedId(session.id)}>
                    {session.visitorName}
                  </Button>
                </li>
              ))}
            </ul>
          </section>
        </div>
        <div>
          {selected || ended ? (
            <ChatPanel
              messages={messages}
              mine="agent"
              ended={ended}
              onSend={(body) => connection.current!.send(selectedId!, body)}
              onEnd={() => void connection.current?.end(selectedId!)}
            />
          ) : (
            <p className="text-muted-foreground">{t('chat.selectChat')}</p>
          )}
        </div>
      </div>
    </div>
  )
}
