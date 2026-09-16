import type { CSSProperties } from 'react'

const CIRCUMFERENCE = 2 * Math.PI * 42

/**
 * Radial countdown. Presentation only: `seconds` and `fraction` come from GamePage's existing
 * countdown (server deadline + secondsPerQuestion). The number is not a live region; screen readers get
 * the timer label on demand plus one polite announcement when the last seconds start.
 */
export default function GameTimer({
  seconds,
  fraction,
  urgent,
  expired,
  className = '',
}: {
  seconds: number
  fraction: number
  urgent: boolean
  expired: boolean
  className?: string
}) {
  const warning = urgent && !expired // the pulse only runs while time is left
  const ring = urgent ? 'var(--color-bad)' : fraction > 0.7 ? 'var(--color-ok)' : 'var(--color-sun)'
  return (
    <div className={`relative justify-self-end ${className}`}>
      <div
        role="timer"
        aria-label={`Qalan vaxt ${seconds} saniyə`}
        data-urgent={urgent ? 'true' : 'false'}
        data-testid="game-timer"
        className={`relative grid size-[clamp(5.2rem,min(7.5vw,12.5vh),8.5rem)] place-items-center rounded-full bg-ink-900 shadow-[0_6px_0_#070519,0_0_2.5rem_-0.5rem_var(--ring)] max-sm:size-[4rem] ${warning ? 'timer-urgent' : ''}`}
        style={{ '--ring': ring } as CSSProperties}
      >
        <svg viewBox="0 0 100 100" className="absolute inset-0 size-full -rotate-90" aria-hidden="true">
          <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="9" />
          <circle
            cx="50"
            cy="50"
            r="42"
            fill="none"
            stroke={ring}
            strokeWidth="9"
            strokeLinecap="round"
            strokeDasharray={CIRCUMFERENCE}
            strokeDashoffset={CIRCUMFERENCE * (1 - fraction)}
            style={{ transition: 'stroke-dashoffset 200ms linear, stroke 300ms ease' }}
          />
        </svg>
        <span
          data-testid="countdown"
          aria-hidden="true"
          className={`relative font-display text-[clamp(1.9rem,min(3vw,5vh),3.3rem)] font-extrabold leading-none tabular-nums max-sm:text-[1.45rem] ${urgent ? 'text-bad' : 'text-fg'}`}
        >
          {/* Keyed by the value so each new second replays the tick. */}
          <span key={seconds} className="tick">{seconds}</span>
        </span>
      </div>
      <span className="sr-only" aria-live="polite" data-testid="timer-announcement">
        {warning ? 'Son saniyələr' : ''}
      </span>
    </div>
  )
}
