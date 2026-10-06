import { useTranslation } from 'react-i18next'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

export interface BreakdownRow {
  key: string
  label: string
  count: number
}

interface BreakdownTableProps {
  title: string
  /** Heading of the first column (what is counted by). */
  labelHeading: string
  rows: BreakdownRow[]
}

/** A titled table of counts (one breakdown of a report). */
export function BreakdownTable({ title, labelHeading, rows }: BreakdownTableProps) {
  const { t } = useTranslation()

  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <Table aria-label={title}>
          <TableHeader>
            <TableRow>
              <TableHead>{labelHeading}</TableHead>
              <TableHead className="text-end">{t('reports.count')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={row.key}>
                <TableCell>{row.label}</TableCell>
                <TableCell className="text-end tabular-nums">{row.count}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  )
}
