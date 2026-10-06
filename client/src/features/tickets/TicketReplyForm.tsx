import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { addTicketMessage } from '@/api/tickets'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { InsertArticleControl } from '@/features/knowledge-base/InsertArticleControl'
import { ticketsQueryKey } from './useTickets'

/** Reply box of a ticket: a reply to the customer, or (toggle) an internal note the customer never sees. */
export function TicketReplyForm({ ticketId }: { ticketId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [internal, setInternal] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [template, setTemplate] = useState('')
  const [templateOffered, setTemplateOffered] = useState(false)

  const send = useMutation({
    mutationFn: (body: string) =>
      addTicketMessage(ticketId, template.trim() ? { body, internal, templateName: template.trim() } : { body, internal }),
    onSuccess: async () => {
      toast.success(t(internal ? 'tickets.details.noteAdded' : 'tickets.details.replySent'))
      setText('')
      setTemplate('')
      setTemplateOffered(false)
      setError(null)
      await queryClient.invalidateQueries({ queryKey: ticketsQueryKey })
    },
  })

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    const body = text.trim()
    if (!body) {
      setError(t('tickets.details.messageRequired'))
      return
    }
    try {
      await send.mutateAsync(body)
    } catch (caught) {
      const problem = isApiError(caught) ? caught.problem?.errors : undefined
      const message = problem?.body?.[0] ?? problem?.status?.[0] ?? problem?.channel?.[0]
      if (problem?.body?.[0]) setTemplateOffered(true) // e.g. the WhatsApp 24-hour window: a template can still be sent
      if (message) setError(message)
    }
  }

  return (
    <form noValidate onSubmit={onSubmit}>
      <FieldGroup>
        <Field data-invalid={error !== null}>
          <FieldLabel htmlFor="ticket-message">{t('tickets.details.message')}</FieldLabel>
          <Textarea
            id="ticket-message"
            dir="auto"
            rows={4}
            value={text}
            aria-invalid={error !== null}
            onChange={(event) => setText(event.target.value)}
          />
          {error ? <FieldError errors={[{ message: error }]} /> : null}
        </Field>
        <InsertArticleControl
          ticketId={ticketId}
          onInsert={(inserted) => setText((current) => `${current.trimEnd()}${inserted}`)}
        />
        {templateOffered ? (
          <Field>
            <FieldLabel htmlFor="ticket-message-template">{t('tickets.details.templateName')}</FieldLabel>
            <Input
              id="ticket-message-template"
              dir="ltr"
              value={template}
              onChange={(event) => setTemplate(event.target.value)}
            />
          </Field>
        ) : null}
        <Field orientation="horizontal">
          <Checkbox id="ticket-message-internal" checked={internal} onCheckedChange={(checked) => setInternal(checked === true)} />
          <FieldLabel htmlFor="ticket-message-internal">{t('tickets.details.internalToggle')}</FieldLabel>
        </Field>
        <div>
          <Button type="submit" disabled={send.isPending}>
            {send.isPending
              ? t('tickets.details.sending')
              : internal
                ? t('tickets.details.addNote')
                : t('tickets.details.sendReply')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}
