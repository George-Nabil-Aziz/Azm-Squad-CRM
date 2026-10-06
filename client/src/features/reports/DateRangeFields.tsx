import { useTranslation } from 'react-i18next'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'

interface DateRangeFieldsProps {
  from: string
  to: string
  onFromChange: (value: string) => void
  onToChange: (value: string) => void
}

/** From / To day pickers shared by every report (whole UTC days; empty = the server default of the last 30 days). */
export function DateRangeFields({ from, to, onFromChange, onToChange }: DateRangeFieldsProps) {
  const { t } = useTranslation()

  return (
    <>
      <Field>
        <FieldLabel htmlFor="report-from">{t('reports.filters.from')}</FieldLabel>
        <Input id="report-from" type="date" value={from} max={to || undefined} onChange={(event) => onFromChange(event.target.value)} />
      </Field>
      <Field>
        <FieldLabel htmlFor="report-to">{t('reports.filters.to')}</FieldLabel>
        <Input id="report-to" type="date" value={to} min={from || undefined} onChange={(event) => onToChange(event.target.value)} />
      </Field>
    </>
  )
}
