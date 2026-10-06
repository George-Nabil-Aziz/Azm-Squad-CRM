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
import { QuickReplyPicker } from './QuickReplyPicker'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { ticketsQueryKey, useTicketAssignees } from './useTickets'

/** Reply box of a ticket: a reply to the customer, or (toggle) an internal note the customer never sees. */
export function TicketReplyForm({ ticketId }: { ticketId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [internal, setInternal] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [template, setTemplate] = useState('')
  const [templateOffered, setTemplateOffered] = useState(false)
  const [mentions, setMentions] = useState<{ id: string; name: string }[]>([])
  const assignees = useTicketAssignees()

  const send = useMutation({
    mutationFn: (body: string) => {
      // Only colleagues whose @Name is still in the note are mentioned, and only internal notes mention anybody.
      const mentionedUserIds = internal ? mentions.filter((m) => body.includes(`@${m.name}`)).map((m) => m.id) : []
      return addTicketMessage(ticketId, {
        body,
        internal,
        ...(template.trim() ? { templateName: template.trim() } : {}),
        ...(mentionedUserIds.length > 0 ? { mentionedUserIds } : {}),
      })
    },
    onSuccess: async () => {
      toast.success(t(internal ? 'tickets.details.noteAdded' : 'tickets.details.replySent'))
      setText('')
      setMentions([])
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
        <QuickReplyPicker ticketId={ticketId} onInsert={(inserted) => setText((current) => (current.trim() ? `${current}\n${inserted}` : inserted))} />
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
        {internal ? (
          <Field>
            <FieldLabel htmlFor="ticket-message-mention">{t('tickets.details.mention')}</FieldLabel>
            <NativeSelect
              id="ticket-message-mention"
              value=""
              onChange={(event) => {
                const colleague = assignees.data?.find((a) => a.id === event.target.value)
                if (!colleague) return
                setMentions((current) => [...current.filter((m) => m.id !== colleague.id), { id: colleague.id, name: colleague.fullName }])
                setText((current) => `${current}${current && !current.endsWith(' ') ? ' ' : ''}@${colleague.fullName} `)
              }}
            >
              <NativeSelectOption value="">{t('tickets.details.mentionPlaceholder')}</NativeSelectOption>
              {assignees.data?.map((assignee) => (
                <NativeSelectOption key={assignee.id} value={assignee.id}>
                  {assignee.fullName}
                </NativeSelectOption>
              ))}
            </NativeSelect>
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
