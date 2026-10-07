/** A "#RGB" / "#RRGGBB" colour as [r, g, b] (0-255), or null when it is not one. */
export function parseHexColor(color: string): [number, number, number] | null {
  const match = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(color.trim())
  if (!match) return null
  const hex = match[1].length === 3 ? [...match[1]].map((digit) => digit + digit).join('') : match[1]
  return [0, 2, 4].map((start) => parseInt(hex.slice(start, start + 2), 16)) as [number, number, number]
}

/** WCAG relative luminance (0 = black, 1 = white). */
function luminance([r, g, b]: [number, number, number]): number {
  const channel = (value: number) => {
    const s = value / 255
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4
  }
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b)
}

/** The theme's dark and light text colours (the shadcn defaults), picked for contrast on a brand colour. */
export const DARK_FOREGROUND = 'oklch(0.145 0 0)'
export const LIGHT_FOREGROUND = 'oklch(0.985 0 0)'

/** The text colour that reads best on `color` (light text on dark colours and the other way round). */
export function contrastForeground(color: string): string {
  const rgb = parseHexColor(color)
  if (!rgb) return LIGHT_FOREGROUND
  return luminance(rgb) > 0.4 ? DARK_FOREGROUND : LIGHT_FOREGROUND
}
