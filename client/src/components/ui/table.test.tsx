import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from './table'

function Sample() {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell>Nour</TableCell>
          <TableCell>Active</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  )
}

describe('Table on small screens (scrolls in place)', () => {
  it('wraps the table in a container that scrolls horizontally and can shrink inside flex/grid parents', () => {
    render(<Sample />)

    const container = screen.getByRole('table').parentElement!
    expect(container).toHaveAttribute('data-slot', 'table-container')
    expect(container).toHaveClass('overflow-x-auto', 'w-full', 'max-w-full', 'min-w-0')
  })

  it('keeps a real table on every screen size: no card layout classes, header stays visible', () => {
    render(<Sample />)

    const html = document.body.innerHTML
    expect(html).not.toContain('max-md:')
    expect(html).not.toContain('data-label')
    expect(screen.getAllByRole('columnheader')[0].closest('thead')).not.toHaveClass('sr-only')
  })

  it('keeps cells on one line and gives the table a sensible minimum width', () => {
    render(<Sample />)

    expect(screen.getByRole('cell', { name: 'Nour' })).toHaveClass('whitespace-nowrap')
    expect(screen.getByRole('table')).toHaveClass('min-w-[32rem]')
  })

  it('uses only logical (RTL-safe) spacing', () => {
    render(<Sample />)

    expect(document.body.innerHTML).not.toMatch(/(^|[\s"':])(ml|mr|pl|pr|left|right)-/)
  })
})
