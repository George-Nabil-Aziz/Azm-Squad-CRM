/** Soft tinted colour pairs from the theme tokens (`--tone-*` and `--tone-*-soft` in index.css); full class names so Tailwind finds them. */
export const toneNames = ['indigo', 'sky', 'teal', 'emerald', 'amber', 'orange', 'rose', 'violet', 'slate'] as const

export type Tone = (typeof toneNames)[number]

/** Text and soft background of a tone, for badges and chips. */
export const toneSoftClasses: Record<Tone, string> = {
  indigo: 'bg-tone-indigo-soft text-tone-indigo',
  sky: 'bg-tone-sky-soft text-tone-sky',
  teal: 'bg-tone-teal-soft text-tone-teal',
  emerald: 'bg-tone-emerald-soft text-tone-emerald',
  amber: 'bg-tone-amber-soft text-tone-amber',
  orange: 'bg-tone-orange-soft text-tone-orange',
  rose: 'bg-tone-rose-soft text-tone-rose',
  violet: 'bg-tone-violet-soft text-tone-violet',
  slate: 'bg-tone-slate-soft text-tone-slate',
}

/** Accent bar colour (top edge) of a tone, for cards. */
export const toneBarClasses: Record<Tone, string> = {
  indigo: 'bg-tone-indigo',
  sky: 'bg-tone-sky',
  teal: 'bg-tone-teal',
  emerald: 'bg-tone-emerald',
  amber: 'bg-tone-amber',
  orange: 'bg-tone-orange',
  rose: 'bg-tone-rose',
  violet: 'bg-tone-violet',
  slate: 'bg-tone-slate',
}
