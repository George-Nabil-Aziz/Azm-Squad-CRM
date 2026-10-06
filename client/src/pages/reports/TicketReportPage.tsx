import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { exportTicketReport, type ExportFormat, type TicketReportParams } from '@/api/reports'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldLabel } from '@/components/ui/field'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { saveFile } from '@/lib/save-file'
import { BreakdownTable } from '@/features/reports/BreakdownTable'
import { DateRangeFields } from '@/features/reports/DateRangeFields'
import { ExportButtons } from '@/features/reports/ExportButtons'
import { useTicketReport } from '@/features/reports/useTicketReport'
import { useTicketCategories } from '@/features/ticket-categories/useTicketCategories'
import {
  ticketChannels,
  ticketPriorities,
  ticketStatuses,
  type TicketChannel,
  type TicketPriority,
  type TicketStatus,
} from '@/features/tickets/ticket-values'

interface Filters {
  from: string
  to: string
  status: TicketStatus | ''
  categoryId: string
  channel: TicketChannel | ''
  priority: TicketPriority | ''
}

const noFilters: Filters = { from: '', to: '', status: '', categoryId: '', channel: '', priority: '' }

/** Only the filters that are set, as report parameters. */
function toParams(filters: Filters): TicketReportParams {
  const params: TicketReportParams = {}
  if (filters.from) params.from = filters.from
  if (filters.to) params.to = filters.to
  if (filters.status) params.status = filters.status
  if (filters.categoryId) params.categoryId = filters.categoryId
  if (filters.channel) params.channel = filters.channel
  if (filters.priority) params.priority = filters.priority
  return params
}

/** Ticket volume report: counts by status, category, channel, priority and day for a date range, with filters and export. */
export function TicketReportPage() {
  const { t } = useTranslation()
  const [filters, setFilters] = useState<Filters>(noFilters)
  const params = toParams(filters)
  const report = useTicketReport(params)
  const categories = useTicketCategories({})

  function set<K extends keyof Filters>(key: K, value: Filters[K]) {
    setFilters((current) => ({ ...current, [key]: value }))
  }

  async function onExport(format: ExportFormat) {
    const blob = await exportTicketReport(params, format)
    const range = report.data ? `${report.data.from}_${report.data.to}` : 'export'
    saveFile(blob, `ticket-report-${range}.${format}`)
  }

  const data = report.data

  return (
    <div className="flex flex-col gap-6">
      <div className="grid items-end gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7">
        <DateRangeFields
          from={filters.from}
          to={filters.to}
          onFromChange={(value) => set('from', value)}
          onToChange={(value) => set('to', value)}
        />
        <Field>
          <FieldLabel htmlFor="report-status">{t('reports.filters.status')}</FieldLabel>
          <NativeSelect
            id="report-status"
            className="w-full"
            value={filters.status}
            onChange={(event) => set('status', event.target.value as TicketStatus | '')}
          >
            <NativeSelectOption value="">{t('reports.filters.all')}</NativeSelectOption>
            {ticketStatuses.map((status) => (
              <NativeSelectOption key={status} value={status}>
                {t(`tickets.statuses.${status}`)}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </Field>
        <Field>
          <FieldLabel htmlFor="report-category">{t('reports.filters.category')}</FieldLabel>
          <NativeSelect
            id="report-category"
            className="w-full"
            value={filters.categoryId}
            onChange={(event) => set('categoryId', event.target.value)}
          >
            <NativeSelectOption value="">{t('reports.filters.all')}</NativeSelectOption>
            {categories.data?.map((category) => (
              <NativeSelectOption key={category.id} value={category.id}>
                {category.name}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </Field>
        <Field>
          <FieldLabel htmlFor="report-channel">{t('reports.filters.channel')}</FieldLabel>
          <NativeSelect
            id="report-channel"
            className="w-full"
            value={filters.channel}
            onChange={(event) => set('channel', event.target.value as TicketChannel | '')}
          >
            <NativeSelectOption value="">{t('reports.filters.all')}</NativeSelectOption>
            {ticketChannels.map((channel) => (
              <NativeSelectOption key={channel} value={channel}>
                {t(`tickets.channels.${channel}`)}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </Field>
        <Field>
          <FieldLabel htmlFor="report-priority">{t('reports.filters.priority')}</FieldLabel>
          <NativeSelect
            id="report-priority"
            className="w-full"
            value={filters.priority}
            onChange={(event) => set('priority', event.target.value as TicketPriority | '')}
          >
            <NativeSelectOption value="">{t('reports.filters.all')}</NativeSelectOption>
            {ticketPriorities.map((priority) => (
              <NativeSelectOption key={priority} value={priority}>
                {t(`tickets.priorities.${priority}`)}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </Field>
        <Button type="button" variant="outline" onClick={() => setFilters(noFilters)}>
          {t('reports.filters.clear')}
        </Button>
      </div>

      <ExportButtons onExport={onExport} />

      {report.isPending || !data ? (
        <p className="text-muted-foreground">{t('reports.loading')}</p>
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle>{t('reports.tickets.total')}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-1">
              <p className="text-4xl font-semibold tabular-nums">{data.total}</p>
              <p className="text-sm text-muted-foreground">{t('reports.tickets.range', { from: data.from, to: data.to })}</p>
            </CardContent>
          </Card>
          <div className="grid gap-4 lg:grid-cols-2">
            <BreakdownTable
              title={t('reports.tickets.byStatus')}
              labelHeading={t('reports.filters.status')}
              rows={data.byStatus.map((row) => ({ key: row.key, label: t(`tickets.statuses.${row.key}`), count: row.count }))}
            />
            <BreakdownTable
              title={t('reports.tickets.byCategory')}
              labelHeading={t('reports.filters.category')}
              rows={data.byCategory.map((row) => ({
                key: row.categoryId ?? 'none',
                label: row.name ?? t('reports.tickets.uncategorized'),
                count: row.count,
              }))}
            />
            <BreakdownTable
              title={t('reports.tickets.byChannel')}
              labelHeading={t('reports.filters.channel')}
              rows={data.byChannel.map((row) => ({ key: row.key, label: t(`tickets.channels.${row.key}`), count: row.count }))}
            />
            <BreakdownTable
              title={t('reports.tickets.byPriority')}
              labelHeading={t('reports.filters.priority')}
              rows={data.byPriority.map((row) => ({ key: row.key, label: t(`tickets.priorities.${row.key}`), count: row.count }))}
            />
          </div>
          <BreakdownTable
            title={t('reports.tickets.byDay')}
            labelHeading={t('reports.tickets.day')}
            rows={data.byDay.map((row) => ({ key: row.date, label: row.date, count: row.count }))}
          />
        </>
      )}
    </div>
  )
}
