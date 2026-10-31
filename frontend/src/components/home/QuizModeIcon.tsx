import type { ReactElement, SVGProps } from 'react'

type IconRenderer = (props: SVGProps<SVGSVGElement>) => ReactElement

const STROKE = { fill: 'none', stroke: 'currentColor', strokeWidth: 1.6, strokeLinecap: 'round', strokeLinejoin: 'round' } as const

const Globe: IconRenderer = (props) => (
  <svg viewBox="0 0 24 24" {...STROKE} {...props}>
    <circle cx="12" cy="12" r="9" />
    <path d="M3 12h18M12 3c2.5 2.6 3.8 5.8 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.8-3.8-9s1.3-6.4 3.8-9Z" />
  </svg>
)

const Book: IconRenderer = (props) => (
  <svg viewBox="0 0 24 24" {...STROKE} {...props}>
    {/* Open book: two curved pages meeting at a centre spine, with a couple of text-line hints. */}
    <path d="M12 6.2C10.3 4.9 7.8 4.3 4.5 4.6v13.6c3.3-.3 5.8.3 7.5 1.6" />
    <path d="M12 6.2c1.7-1.3 4.2-1.9 7.5-1.6v13.6c-3.3-.3-5.8.3-7.5 1.6Z" />
    <path d="M7 8.6c1.4-.2 2.7 0 3.6.5M7 12c1.4-.2 2.7 0 3.6.5" />
  </svg>
)

const Feather: IconRenderer = (props) => (
  <svg viewBox="0 0 24 24" {...STROKE} {...props}>
    <path d="M20.5 3.5c-4 0-13 2-15.5 14.5l-1.5 3 3-1.5C19 17 20.5 8 20.5 3.5Z" />
    <path d="M15.5 8.5 5 19M18 6l-4.5 4.5" />
  </svg>
)

const Leaf: IconRenderer = (props) => (
  <svg viewBox="0 0 24 24" {...STROKE} {...props}>
    <path d="M20 4C10 4 4 10 4 18v2h2c8 0 14-6 14-16Z" />
    <path d="M6 20C10 14 14 10 19 5" />
  </svg>
)

/** Safe fallback for an unknown or missing iconKey: a generic question-mark glyph in the same style. */
const Fallback: IconRenderer = (props) => (
  <svg viewBox="0 0 24 24" {...STROKE} {...props}>
    <circle cx="12" cy="12" r="9" />
    <path d="M9.6 9.4c0-1.6 1.2-2.8 2.6-2.8s2.6 1 2.6 2.4c0 1.5-1 2-1.9 2.6-.7.5-1 .9-1 1.7" />
    <circle cx="11.9" cy="16.6" r="0.2" fill="currentColor" stroke="none" />
  </svg>
)

const Sprout: IconRenderer = (props) => (
  <svg viewBox="0 0 24 24" {...STROKE} {...props}>
    {/* A seedling in its pot: the nursery, as against the single leaf that stands for the city's trees. */}
    <path d="M12 13.5V9" />
    <path d="M12 9C12 6.5 10 4.5 7 4.5c0 3 2 4.5 5 4.5Z" />
    <path d="M12 10.5c0-2.2 1.8-4 4.5-4 0 2.7-1.8 4-4.5 4Z" />
    <path d="M5.5 13.5h13l-1.2 5.3a1.6 1.6 0 0 1-1.6 1.2H8.3a1.6 1.6 0 0 1-1.6-1.2L5.5 13.5Z" />
  </svg>
)

/** Fixed allowlist: every QuizMode.iconKey the backend can send. Never resolves to a URL, raw HTML or a dynamic import. */
const ICONS: Record<string, IconRenderer> = {
  globe: Globe,
  book: Book,
  feather: Feather,
  leaf: Leaf,
  sprout: Sprout,
}

/** Renders a QuizMode.iconKey as a local SVG glyph; an unknown key falls back to a generic icon, never an error. */
export default function QuizModeIcon({ iconKey, className = '' }: { iconKey: string; className?: string }) {
  const Icon = ICONS[iconKey] ?? Fallback
  return <Icon aria-hidden="true" className={className} />
}
