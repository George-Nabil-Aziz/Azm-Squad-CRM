import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { ChatMessage, ChatSend } from '@/api/chat'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

interface ChatPanelProps {
  messages: ChatMessage[]
  /** Which sender is "me" (visitor widget: visitor; agent console: agent). */
  mine: ChatMessage['sender']
  ended: boolean
  onSend: ChatSend
  onEnd: () => void
}

/** The conversation: messages, a send box and the end button. Used by the agent console and the visitor widget. */
export function ChatPanel({ messages, mine, ended, onSend, onEnd }: ChatPanelProps) {
  const { t } = useTranslation()
  const [text, setText] = useState('')

  async function submit(event: FormEvent) {
    event.preventDefault()
    const body = text.trim()
    if (!body) return
    setText('')
    try {
      await onSend(body)
    } catch {
      setText(body) // not sent (chat ended, connection lost): keep the text
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <ul aria-label={t('chat.messages')} className="flex max-h-96 min-h-48 flex-col gap-2 overflow-y-auto rounded-lg border p-3">
        {messages.map((message) => (
          <li
            key={message.id}
            className={`max-w-[80%] rounded-lg px-3 py-2 text-sm ${
              message.sender === mine ? 'self-end bg-primary text-primary-foreground' : 'self-start bg-muted'
            }`}
          >
            <span className="block text-xs opacity-70">{message.senderName}</span>
            <span dir="auto">{message.body}</span>
          </li>
        ))}
      </ul>
      {ended ? (
        <p role="status" className="text-sm text-muted-foreground">
          {t('chat.endedNotice')}
        </p>
      ) : (
        <form onSubmit={submit} className="flex gap-2">
          <Input aria-label={t('chat.messageLabel')} value={text} onChange={(event) => setText(event.target.value)} dir="auto" />
          <Button type="submit">{t('chat.send')}</Button>
          <Button type="button" variant="outline" onClick={onEnd}>
            {t('chat.end')}
          </Button>
        </form>
      )}
    </div>
  )
}
