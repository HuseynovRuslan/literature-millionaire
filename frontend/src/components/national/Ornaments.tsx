/**
 * National motifs for the paper-toned kiosk screens: buta, eight-pointed star (octagram)
 * and a thin carpet border. Adapted from the cingiz branch (Ornaments.jsx) into TypeScript;
 * colours come from the `.paper` CSS variables so nothing leaks into the dark game screens.
 * Everything here is decorative and hidden from assistive technology.
 */
import type { CSSProperties } from 'react'

const BUTA_D =
  'M50 135 C18 135 5 105 12 78 C18 52 38 38 48 22 C54 12 62 4 74 6 C86 8 88 22 80 26 C92 48 96 78 88 102 C82 122 68 135 50 135 Z'
const BUTA_MID_D =
  'M50 124 C27 124 17 103 22 83 C27 63 42 52 52 38 C58 30 64 24 70 22 C80 44 84 70 79 94 C75 112 64 124 50 124 Z'
const BUTA_IN_D = 'M50 113 C34 113 28 99 31 86 C34 72 45 63 54 52 C62 62 70 78 69 92 C68 105 60 113 50 113 Z'

interface MotifProps {
  className?: string
  style?: CSSProperties
  flip?: boolean
}

export function Buta({ className = '', style, flip = false }: MotifProps) {
  return (
    <svg
      viewBox="0 0 100 140"
      className={className}
      style={{ transform: flip ? 'scaleX(-1)' : undefined, ...style }}
      aria-hidden="true"
    >
      <path d={BUTA_D} fill="var(--p-gold)" />
      <path d={BUTA_MID_D} fill="var(--p-burgundy)" />
      <path d={BUTA_IN_D} fill="var(--p-gold)" />
      <circle cx="50" cy="92" r="10" fill="var(--p-burgundy)" />
      <circle cx="50" cy="92" r="6" fill="var(--p-teal)" />
      <circle cx="50" cy="92" r="2.4" fill="var(--p-gold)" />
      <path d="M62 46 C68 52 72 60 73 68" stroke="var(--p-teal)" strokeWidth="2.4" fill="none" strokeLinecap="round" />
    </svg>
  )
}

export function Octagram({ className = '', style, inner = true }: MotifProps & { inner?: boolean }) {
  return (
    <svg viewBox="0 0 100 100" className={className} style={style} aria-hidden="true">
      <g fill="var(--p-paper)" stroke="var(--p-gold)" strokeWidth="2.5" strokeLinejoin="round">
        <rect x="17" y="17" width="66" height="66" />
        <rect x="17" y="17" width="66" height="66" transform="rotate(45 50 50)" />
      </g>
      {inner && <circle cx="50" cy="50" r="18" fill="none" stroke="var(--p-burgundy)" strokeWidth="2" />}
    </svg>
  )
}

/** Carpet "göl" (stepped medallion), outline only. */
export function Gol({ className = '', style }: MotifProps) {
  return (
    <svg viewBox="0 0 100 100" className={className} style={style} aria-hidden="true" fill="none" stroke="var(--p-burgundy)">
      <path
        d="M50 6 L58 14 L58 22 L66 22 L78 34 L78 42 L86 42 L94 50 L86 58 L78 58 L78 66 L66 78 L58 78 L58 86 L50 94 L42 86 L42 78 L34 78 L22 66 L22 58 L14 58 L6 50 L14 42 L22 42 L22 34 L34 22 L42 22 L42 14 Z"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      <path d="M50 28 L72 50 L50 72 L28 50 Z" strokeWidth="2.2" />
    </svg>
  )
}

/** Heading rule: buta, line, star, line, buta. */
export function ButaRule({ className = '' }: { className?: string }) {
  return (
    <div className={`flex items-center gap-4 ${className}`} aria-hidden="true">
      <Buta className="h-8 w-6 shrink-0" flip />
      <span className="h-[2px] flex-1 bg-gradient-to-r from-transparent via-[var(--p-gold)] to-transparent" />
      <Octagram className="h-6 w-6 shrink-0" inner={false} />
      <span className="h-[2px] flex-1 bg-gradient-to-r from-transparent via-[var(--p-gold)] to-transparent" />
      <Buta className="h-8 w-6 shrink-0" />
    </div>
  )
}
