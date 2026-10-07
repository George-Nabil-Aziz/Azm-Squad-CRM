import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  createWebhook,
  deleteWebhook,
  listWebhookDeliveries,
  listWebhooks,
  setWebhookEnabled,
  webhookEvents,
  type CreatedWebhook,
  type Webhook,
  type WebhookEvent,
} from '@/api/integrations'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

const webhooksQueryKey = ['webhooks'] as const

/** i18n key of each event label (a dot cannot be part of a translation key name). */
const eventLabelKey = {
  'ticket.created': 'integrations.webhooks.eventLabels.ticketCreated',
  'ticket.resolved': 'integrations.webhooks.eventLabels.ticketResolved',
} as const satisfies Record<WebhookEvent, string>

const statusLabelKey = {
  pending: 'integrations.webhooks.deliveryStatus.pending',
  delivered: 'integrations.webhooks.deliveryStatus.delivered',
  failed: 'integrations.webhooks.deliveryStatus.failed',
} as const

function isHttpUrl(value: string) {
  try {
    const url = new URL(value.trim())
    return url.protocol === 'http:' || url.protocol === 'https:'
  } catch {
    return false
  }
}

/** Outgoing webhooks (CRM-59): register, enable / disable, delete and the delivery log. The secret is shown once. */
export function WebhooksPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const webhooks = useQuery({ queryKey: webhooksQueryKey, queryFn: ({ signal }) => listWebhooks(signal) })
  const [name, setName] = useState('')
  const [url, setUrl] = useState('')
  const [events, setEvents] = useState<WebhookEvent[]>([])
  const [errors, setErrors] = useState<{ name?: string; url?: string; events?: string }>({})
  const [created, setCreated] = useState<CreatedWebhook | null>(null)
  const [selected, setSelected] = useState<Webhook | null>(null)
  const reload = () => queryClient.invalidateQueries({ queryKey: webhooksQueryKey })

  const create = useMutation({ mutationFn: (request: { name: string; url: string; events: WebhookEvent[] }) => createWebhook(request), onSuccess: reload })
  const toggle = useMutation({
    mutationFn: (request: { id: string; enabled: boolean }) => setWebhookEnabled(request.id, request.enabled),
    onSuccess: async () => {
      await reload()
      await queryClient.invalidateQueries({ queryKey: ['webhook-deliveries'] })
    },
  })
  const remove = useMutation({
    mutationFn: (id: string) => deleteWebhook(id),
    onSuccess: async (_data, id) => {
      setSelected((current) => (current?.id === id ? null : current))
      await reload()
    },
  })
  const deliveries = useQuery({
    queryKey: ['webhook-deliveries', selected?.id],
    queryFn: ({ signal }) => listWebhookDeliveries(selected!.id, signal),
    enabled: selected !== null,
  })

  async function submit(event: FormEvent) {
    event.preventDefault()
    const next = {
      name: name.trim() ? undefined : t('integrations.webhooks.nameRequired'),
      url: isHttpUrl(url) ? undefined : t('integrations.webhooks.urlInvalid'),
      events: events.length > 0 ? undefined : t('integrations.webhooks.eventsRequired'),
    }
    setErrors(next)
    if (next.name || next.url || next.events) return
    try {
      setCreated(await create.mutateAsync({ name: name.trim(), url: url.trim(), events }))
      setName('')
      setUrl('')
      setEvents([])
    } catch {
      // The API client already shows the error as a toast.
    }
  }

  function toggleEvent(eventName: WebhookEvent, checked: boolean) {
    setEvents((current) => (checked ? [...current, eventName] : current.filter((e) => e !== eventName)))
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-muted-foreground">{t('integrations.webhooks.description')}</p>

      {created ? (
        <div role="status" className="flex max-w-xl flex-col gap-2 rounded-md border bg-muted p-4">
          <p className="text-sm font-medium">{t('integrations.webhooks.created')}</p>
          <label htmlFor="created-webhook-secret" className="text-sm">
            {t('integrations.webhooks.secretLabel')}
          </label>
          <Input id="created-webhook-secret" dir="ltr" readOnly value={created.secret} onFocus={(e) => e.target.select()} />
          <div>
            <Button type="button" variant="outline" onClick={() => setCreated(null)}>
              {t('integrations.webhooks.dismiss')}
            </Button>
          </div>
        </div>
      ) : null}

      <form onSubmit={(event) => void submit(event)} noValidate className="flex max-w-xl flex-col gap-3">
        <div className="flex flex-col gap-1">
          <label htmlFor="webhook-name" className="text-sm font-medium">
            {t('integrations.webhooks.name')}
          </label>
          <Input id="webhook-name" value={name} aria-invalid={errors.name ? true : undefined} onChange={(e) => setName(e.target.value)} />
          {errors.name ? <p role="alert" className="text-sm text-destructive">{errors.name}</p> : null}
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="webhook-url" className="text-sm font-medium">
            {t('integrations.webhooks.url')}
          </label>
          <Input id="webhook-url" dir="ltr" value={url} aria-invalid={errors.url ? true : undefined} onChange={(e) => setUrl(e.target.value)} />
          {errors.url ? <p role="alert" className="text-sm text-destructive">{errors.url}</p> : null}
        </div>
        <fieldset className="flex flex-col gap-2">
          <legend className="text-sm font-medium">{t('integrations.webhooks.events')}</legend>
          {webhookEvents.map((eventName) => (
            <div key={eventName} className="flex items-center gap-2">
              <Checkbox
                id={`webhook-event-${eventName}`}
                checked={events.includes(eventName)}
                onCheckedChange={(checked) => toggleEvent(eventName, checked === true)}
              />
              <label htmlFor={`webhook-event-${eventName}`} className="text-sm">
                {t(eventLabelKey[eventName])}
                <span dir="ltr" className="ms-2 text-xs text-muted-foreground">
                  {eventName}
                </span>
              </label>
            </div>
          ))}
          {errors.events ? <p role="alert" className="text-sm text-destructive">{errors.events}</p> : null}
        </fieldset>
        <div>
          <Button type="submit" disabled={create.isPending}>
            {t('integrations.webhooks.register')}
          </Button>
        </div>
      </form>

      {webhooks.isPending ? <p className="text-muted-foreground">{t('integrations.webhooks.loading')}</p> : null}
      {webhooks.data?.length === 0 ? <p className="text-muted-foreground">{t('integrations.webhooks.empty')}</p> : null}
      {webhooks.data && webhooks.data.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('integrations.webhooks.name')}</TableHead>
              <TableHead>{t('integrations.webhooks.url')}</TableHead>
              <TableHead>{t('integrations.webhooks.events')}</TableHead>
              <TableHead>{t('integrations.webhooks.status')}</TableHead>
              <TableHead className="text-end">{t('integrations.webhooks.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {webhooks.data.map((webhook) => (
              <TableRow key={webhook.id}>
                <TableCell className="font-medium">{webhook.name}</TableCell>
                <TableCell dir="ltr" className="text-start">
                  {webhook.url}
                </TableCell>
                <TableCell>{webhook.events.map((e) => t(eventLabelKey[e])).join(', ')}</TableCell>
                <TableCell>
                  <Badge variant={webhook.isEnabled ? 'secondary' : 'outline'}>
                    {t(webhook.isEnabled ? 'integrations.webhooks.enabled' : 'integrations.webhooks.disabled')}
                  </Badge>
                </TableCell>
                <TableCell>
                  <div className="flex justify-end gap-2">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      aria-label={t('integrations.webhooks.deliveriesFor', { name: webhook.name })}
                      onClick={() => setSelected(webhook)}
                    >
                      {t('integrations.webhooks.deliveries')}
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={toggle.isPending}
                      aria-label={t(webhook.isEnabled ? 'integrations.webhooks.disableFor' : 'integrations.webhooks.enableFor', { name: webhook.name })}
                      onClick={() => toggle.mutate({ id: webhook.id, enabled: !webhook.isEnabled })}
                    >
                      {t(webhook.isEnabled ? 'integrations.webhooks.disable' : 'integrations.webhooks.enable')}
                    </Button>
                    <Button
                      type="button"
                      variant="destructive"
                      size="sm"
                      disabled={remove.isPending}
                      aria-label={t('integrations.webhooks.deleteFor', { name: webhook.name })}
                      onClick={() => remove.mutate(webhook.id)}
                    >
                      {t('integrations.webhooks.delete')}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}

      {selected ? (
        <section aria-label={t('integrations.webhooks.deliveriesFor', { name: selected.name })} className="flex flex-col gap-2">
          <h2 className="text-lg font-semibold">{t('integrations.webhooks.deliveriesFor', { name: selected.name })}</h2>
          {deliveries.isPending ? <p className="text-muted-foreground">{t('integrations.webhooks.deliveriesLoading')}</p> : null}
          {deliveries.data?.length === 0 ? <p className="text-muted-foreground">{t('integrations.webhooks.deliveriesEmpty')}</p> : null}
          {deliveries.data && deliveries.data.length > 0 ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('integrations.webhooks.time')}</TableHead>
                  <TableHead>{t('integrations.webhooks.event')}</TableHead>
                  <TableHead>{t('integrations.webhooks.status')}</TableHead>
                  <TableHead>{t('integrations.webhooks.attempts')}</TableHead>
                  <TableHead>{t('integrations.webhooks.result')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {deliveries.data.map((delivery) => (
                  <TableRow key={delivery.id}>
                    <TableCell>{new Date(delivery.createdAt).toLocaleString()}</TableCell>
                    <TableCell>{t(eventLabelKey[delivery.event])}</TableCell>
                    <TableCell>{t(statusLabelKey[delivery.status])}</TableCell>
                    <TableCell>{delivery.attempts}</TableCell>
                    <TableCell dir="auto">{[delivery.lastStatusCode, delivery.lastError].filter(Boolean).join(' ')}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : null}
        </section>
      ) : null}
    </div>
  )
}
