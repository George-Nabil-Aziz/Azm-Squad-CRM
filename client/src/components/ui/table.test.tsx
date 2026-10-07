import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from './table'

function Sample({ responsive }: { responsive?: boolean }) {
  return (
    <Table responsive={responsive}>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Actions</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell>Nour</TableCell>
          <TableCell>Active</TableCell>
          <TableCell>
            <button type="button">Edit</button>
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>
  )
}

describe('Table on small screens (card layout)', () => {
  it('labels every cell with its column title, so a card row can show it', () => {
    render(<Sample />)

    const row = screen.getByRole('row', { name: /Nour/ })
    const cells = within(row).getAllByRole('cell')
    expect(cells.map((cell) => cell.getAttribute('data-label'))).toEqual(['Name', 'Status', 'Actions'])
  })

  it('turns rows into cards below the md breakpoint and keeps the header for screen readers', () => {
    render(<Sample />)

    expect(screen.getByRole('table')).toHaveClass('max-md:block')
    expect(screen.getByRole('row', { name: /Nour/ })).toHaveClass('max-md:block', 'max-md:rounded-lg', 'max-md:border')
    expect(screen.getByRole('cell', { name: 'Nour' })).toHaveClass('max-md:flex', 'max-md:before:content-[attr(data-label)]')
    expect(screen.getAllByRole('columnheader')[0].closest('thead')).toHaveClass('max-md:sr-only')
  })

  it('uses only logical (RTL-safe) spacing in the card layout', () => {
    render(<Sample />)

    const classes = [
      screen.getByRole('table').className,
      screen.getByRole('row', { name: /Nour/ }).className,
      screen.getByRole('cell', { name: 'Nour' }).className,
    ].join(' ')
    expect(classes).not.toMatch(/(^|[\s:])(ml|mr|pl|pr|left|right)-/)
    expect(classes).not.toMatch(/text-(left|right)/)
  })

  it('stays a plain table when responsive is switched off', () => {
    render(<Sample responsive={false} />)

    expect(screen.getByRole('table')).not.toHaveClass('max-md:block')
    expect(screen.getByRole('cell', { name: 'Nour' })).not.toHaveAttribute('data-label')
  })

  it('labels rows that arrive later', () => {
    const { rerender } = render(<Sample />)

    rerender(
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Status</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow>
            <TableCell>Omar</TableCell>
            <TableCell>Inactive</TableCell>
          </TableRow>
        </TableBody>
      </Table>,
    )

    expect(screen.getByRole('cell', { name: 'Inactive' })).toHaveAttribute('data-label', 'Status')
  })
})
