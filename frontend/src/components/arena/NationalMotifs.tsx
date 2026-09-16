import { useId } from 'react'

/**
 * Light touches of Azerbaijani identity for the arena: the flag's three colours, the eight-pointed star,
 * the buta and a thin carpet band. Decorative only (aria-hidden); they never carry information.
 */

/** Official flag colours. */
const FLAG = { blue: '#00b5e2', red: '#ef3340', green: '#509e2f' } as const

/** A thin blue-red-green ribbon along the top edge of a screen. */
export function FlagStripe({ className = '' }: { className?: string }) {
  return (
    <div className={`pointer-events-none flex h-1 w-full ${className}`} aria-hidden="true">
      <span className="flex-1" style={{ background: FLAG.blue }} />
      <span className="flex-1" style={{ background: FLAG.red }} />
      <span className="flex-1" style={{ background: FLAG.green }} />
    </div>
  )
}

/** Eight-pointed star (two overlapping squares), as on the flag. Uses currentColor. */
export function Octagram({ className = '', outline = false }: { className?: string; outline?: boolean }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true">
      <g fill={outline ? 'none' : 'currentColor'} stroke={outline ? 'currentColor' : 'none'} strokeWidth="1.4" strokeLinejoin="round">
        <rect x="5" y="5" width="14" height="14" />
        <rect x="5" y="5" width="14" height="14" transform="rotate(45 12 12)" />
      </g>
    </svg>
  )
}

/** Buta (paisley). Uses currentColor with an inner curl. */
export function Buta({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 100 140" className={className} aria-hidden="true">
      <path d="M50 135C18 135 5 105 12 78c6-26 26-40 36-56 6-10 14-18 26-16 12 2 14 16 6 20 12 22 16 52 8 76-6 20-20 33-38 33Z" fill="currentColor" />
      <path d="M50 118c-18 0-25-16-21-31 4-15 16-24 25-35 8 10 15 25 14 39-1 15-8 27-18 27Z" fill="none" stroke="rgba(255,255,255,0.45)" strokeWidth="3" />
      <circle cx="50" cy="92" r="7" fill="rgba(255,255,255,0.45)" />
    </svg>
  )
}

/** A narrow repeating carpet border: diamonds between two rules. Colour follows currentColor. */
export function CarpetBand({ className = '' }: { className?: string }) {
  const id = useId().replace(/:/g, '')
  return (
    <svg className={`block h-3 w-full ${className}`} aria-hidden="true" preserveAspectRatio="none">
      <defs>
        <pattern id={`${id}-carpet`} width="24" height="12" patternUnits="userSpaceOnUse">
          <rect y="0.5" width="24" height="1" fill="currentColor" opacity="0.55" />
          <rect y="10.5" width="24" height="1" fill="currentColor" opacity="0.55" />
          <path d="M12 2.5 16 6 12 9.5 8 6Z" fill="currentColor" />
          <path d="M0 4.5 1.5 6 0 7.5ZM24 4.5 22.5 6 24 7.5Z" fill="currentColor" opacity="0.7" />
        </pattern>
      </defs>
      <rect width="100%" height="100%" fill={`url(#${id}-carpet)`} />
    </svg>
  )
}
