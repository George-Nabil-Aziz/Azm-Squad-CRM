import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { CustomerInteraction, InteractionType } from '@/api/customers'
import { Button } from '@/components/ui/button'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { useCustomerTimeline } from './useCustomerTimeline'

const PAGE_SIZE = 10

const filterTypes: readonly InteractionType[] = ['customer', 'note', 'attachment', 'ticket', 'message']

/** Event codes with a label in customers.timeline.events (later stories add theirs here and in en/ar.json). */
const knownEvents = ['customerCreated', 'customerUpdated', 'contactAdded', 'noteAdded', 'attachmentAdded', 'ticketCreated'] as const
type KnownEvent = (typeof knownEvents)[number]

function isKnownEvent(event: string): event is KnownEvent {
  return (knownEvents as readonly string[]).includes(event)
}

interface CustomerTimelineProps {
  customerId: string
}

/** A customer's interaction history: newest first, filter by type, Previous / Next paging. */
export function CustomerTimeline({ customerId }: CustomerTimelineProps) {
  const { t } = useTranslation()
  const [type, setType] = useState<InteractionType | ''>('')
  const [page, setPage] = useState(1)
  const timeline = useCustomerTimeline(customerId, { ...(type ? { type } : {}), page, pageSize: PAGE_SIZE })
  const totalPages = timeline.data ? Math.max(1, Math.ceil(timeline.data.totalCount / timeline.data.pageSize)) : 1

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h2 className="text-lg font-semibold">{t('customers.timeline.title')}</h2>
        <div className="flex items-center gap-2">
          <label htmlFor="timeline-type" className="text-sm text-muted-foreground">
            {t('customers.timeline.filter')}
          </label>
          <NativeSelect
            id="timeline-type"
            value={type}
            onChange={(event) => {
              setType(event.target.value as InteractionType | '')
              setPage(1)
            }}
          >
            <NativeSelectOption value="">{t('customers.timeline.types.all')}</NativeSelectOption>
            {filterTypes.map((filterType) => (
              <NativeSelectOption key={filterType} value={filterType}>
                {t(`customers.timeline.types.${filterType}`)}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
      </div>

      {timeline.isPending ? (
        <p className="text-muted-foreground">{t('customers.timeline.loading')}</p>
      ) : timeline.data && timeline.data.items.length > 0 ? (
        <ol aria-label={t('customers.timeline.title')} className="flex flex-col gap-3">
          {timeline.data.items.map((entry) => (
            <TimelineEntry key={entry.id} entry={entry} />
          ))}
        </ol>
      ) : (
        <p className="text-muted-foreground">{t('customers.timeline.empty')}</p>
      )}

      <div className="flex items-center justify-end gap-2">
        <span className="text-sm text-muted-foreground">{t('customers.timeline.pageInfo', { page, pages: totalPages })}</span>
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          {t('customers.timeline.previous')}
        </Button>
        <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
          {t('customers.timeline.next')}
        </Button>
      </div>
    </section>
  )
}

function TimelineEntry({ entry }: { entry: CustomerInteraction }) {
  const { t, i18n } = useTranslation()
  const time = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(entry.occurredAt),
  )

  return (
    <li className="flex flex-col gap-1 rounded-lg border p-3">
      <h3 className="font-medium">
        {isKnownEvent(entry.event) ? t(`customers.timeline.events.${entry.event}`) : t('customers.timeline.events.other')}
      </h3>
      {entry.details ? (
        <p dir="auto" className="whitespace-pre-line wrap-break-word">
          {entry.details}
        </p>
      ) : null}
      <p className="text-sm text-muted-foreground">
        {entry.actorName ? t('customers.timeline.by', { name: entry.actorName }) : t('customers.timeline.system')}
        {' · '}
        <time dateTime={entry.occurredAt}>{time}</time>
      </p>
    </li>
  )
}
