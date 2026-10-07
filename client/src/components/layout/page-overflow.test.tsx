import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

describe('page shell and wide tables', () => {
  it('lets the page area shrink (min-w-0) so a wide table scrolls inside its own wrapper, not the page', () => {
    render(
      <SidebarProvider>
        <SidebarInset>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell>Nour</TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </SidebarInset>
      </SidebarProvider>,
    )

    const main = screen.getByRole('main')
    expect(main).toHaveClass('min-w-0')
    expect(screen.getByRole('table').parentElement).toHaveClass('overflow-x-auto')
  })
})
