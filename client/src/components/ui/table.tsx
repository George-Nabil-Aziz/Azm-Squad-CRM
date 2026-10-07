import * as React from "react"
import { cn } from "cn"

/**
 * Tables are responsive by default (CRM-64): from the "md" breakpoint up they are normal tables; below it every row becomes a
 * card and each cell shows its column title before its value (the title is read from the header row, see `labelCells`).
 * Pass `responsive={false}` for a table that must stay a table (e.g. a matrix).
 */
const ResponsiveContext = React.createContext(true)

/** Copies the column titles of the header row into `data-label` on the cells of every body row (what the card layout shows). */
function labelCells(table: HTMLTableElement) {
  const titles = Array.from(table.querySelectorAll("thead th")).map(
    (head) => head.textContent?.trim() ?? ""
  )
  table.querySelectorAll("tbody tr").forEach((row) => {
    Array.from(row.children).forEach((cell, index) => {
      const title = titles[index] ?? ""
      if (cell.getAttribute("data-label") !== title) {
        cell.setAttribute("data-label", title)
      }
    })
  })
}

function Table({
  className,
  responsive = true,
  ...props
}: React.ComponentProps<"table"> & { responsive?: boolean }) {
  const ref = React.useRef<HTMLTableElement>(null)

  // After every render: rows and cells come from the parent, so the labels follow them.
  React.useLayoutEffect(() => {
    if (responsive && ref.current) labelCells(ref.current)
  })

  return (
    <ResponsiveContext.Provider value={responsive}>
      <div
        data-slot="table-container"
        className="relative w-full overflow-x-auto"
      >
        <table
          ref={ref}
          data-slot="table"
          data-responsive={responsive ? "true" : undefined}
          className={cn(
            "w-full caption-bottom text-sm",
            responsive && "max-md:block",
            className
          )}
          {...props}
        />
      </div>
    </ResponsiveContext.Provider>
  )
}

function TableHeader({ className, ...props }: React.ComponentProps<"thead">) {
  const responsive = React.useContext(ResponsiveContext)
  return (
    <thead
      data-slot="table-header"
      className={cn(
        "bg-muted/40 [&_tr]:border-b",
        responsive && "max-md:sr-only",
        className
      )}
      {...props}
    />
  )
}

function TableBody({ className, ...props }: React.ComponentProps<"tbody">) {
  const responsive = React.useContext(ResponsiveContext)
  return (
    <tbody
      data-slot="table-body"
      className={cn(
        "[&_tr:last-child]:border-0",
        responsive && "max-md:block max-md:space-y-3 max-md:[&_tr:last-child]:border",
        className
      )}
      {...props}
    />
  )
}

function TableFooter({ className, ...props }: React.ComponentProps<"tfoot">) {
  return (
    <tfoot
      data-slot="table-footer"
      className={cn(
        "border-t bg-muted/50 font-medium [&>tr]:last:border-b-0",
        className
      )}
      {...props}
    />
  )
}

function TableRow({ className, ...props }: React.ComponentProps<"tr">) {
  const responsive = React.useContext(ResponsiveContext)
  return (
    <tr
      data-slot="table-row"
      className={cn(
        "border-b transition-colors hover:bg-muted/50 has-aria-expanded:bg-muted/50 data-[state=selected]:bg-muted",
        responsive && "max-md:block max-md:rounded-lg max-md:border max-md:p-2",
        className
      )}
      {...props}
    />
  )
}

function TableHead({ className, ...props }: React.ComponentProps<"th">) {
  return (
    <th
      data-slot="table-head"
      className={cn(
        "h-10 px-2 text-start align-middle font-medium whitespace-nowrap text-muted-foreground [&:has([role=checkbox])]:pe-0",
        className
      )}
      {...props}
    />
  )
}

function TableCell({ className, ...props }: React.ComponentProps<"td">) {
  const responsive = React.useContext(ResponsiveContext)
  return (
    <td
      data-slot="table-cell"
      className={cn(
        "p-2 align-middle whitespace-nowrap [&:has([role=checkbox])]:pe-0",
        responsive &&
          "max-md:flex max-md:items-center max-md:justify-between max-md:gap-4 max-md:whitespace-normal max-md:break-words max-md:before:shrink-0 max-md:before:text-start max-md:before:font-medium max-md:before:text-muted-foreground max-md:before:content-[attr(data-label)]",
        className
      )}
      {...props}
    />
  )
}

function TableCaption({
  className,
  ...props
}: React.ComponentProps<"caption">) {
  return (
    <caption
      data-slot="table-caption"
      className={cn("mt-4 text-sm text-muted-foreground", className)}
      {...props}
    />
  )
}

export {
  Table,
  TableHeader,
  TableBody,
  TableFooter,
  TableHead,
  TableRow,
  TableCell,
  TableCaption,
}
