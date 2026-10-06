import { DownloadIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ExportFormat } from '@/api/reports'
import { Button } from '@/components/ui/button'

interface ExportButtonsProps {
  /** Downloads the report in the given format; a failure is already reported to the user by the API client. */
  onExport(format: ExportFormat): Promise<void>
}

/** The two download buttons of a report (CSV and Excel). */
export function ExportButtons({ onExport }: ExportButtonsProps) {
  const { t } = useTranslation()
  const [busy, setBusy] = useState<ExportFormat | null>(null)

  async function run(format: ExportFormat) {
    setBusy(format)
    try {
      await onExport(format)
    } catch {
      // The API client already told the user (toast).
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="flex gap-2">
      <Button type="button" variant="outline" disabled={busy !== null} onClick={() => run('csv')}>
        <DownloadIcon aria-hidden="true" />
        {t('reports.exportCsv')}
      </Button>
      <Button type="button" variant="outline" disabled={busy !== null} onClick={() => run('xlsx')}>
        <DownloadIcon aria-hidden="true" />
        {t('reports.exportExcel')}
      </Button>
    </div>
  )
}
