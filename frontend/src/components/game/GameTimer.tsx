import { useId } from 'react'

const CIRCUMFERENCE = 2 * Math.PI * 44

/**
 * Radial countdown medallion. Presentation only: `seconds` and `fraction` come from GamePage's existing
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
  const id = useId().replace(/:/g, '')
  const warning = urgent && !expired // the subtle pulse only runs while time is left
  return (
    <div className={`relative justify-self-end ${className}`}>
      <div
        role="timer"
        aria-label={`Qalan vaxt ${seconds} saniyə`}
        data-urgent={urgent ? 'true' : 'false'}
        data-testid="game-timer"
        className={`relative grid size-[clamp(5.5rem,min(8vw,13vh),9rem)] place-items-center rounded-full max-sm:size-[4.25rem] ${warning ? 'gs-timer-urgent' : ''}`}
      >
        <svg viewBox="0 0 100 100" className="absolute inset-0 size-full -rotate-90" aria-hidden="true">
          <defs>
            <linearGradient id={`${id}-gold`} x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stopColor="#f7df93" />
              <stop offset="1" stopColor="#c9922f" />
            </linearGradient>
            <radialGradient id={`${id}-face`} cx="0.5" cy="0.35" r="0.7">
              <stop offset="0" stopColor="#23407a" />
              <stop offset="1" stopColor="#0e1f44" />
            </radialGradient>
          </defs>
          <circle cx="50" cy="50" r="49" fill={`url(#${id}-face)`} />
          <circle cx="50" cy="50" r="44" fill="none" stroke="rgba(255,255,255,0.16)" strokeWidth="7" />
          <circle
            cx="50"
            cy="50"
            r="44"
            fill="none"
            stroke={urgent ? '#d94452' : `url(#${id}-gold)`}
            strokeWidth="7"
            strokeLinecap="round"
            strokeDasharray={CIRCUMFERENCE}
            strokeDashoffset={CIRCUMFERENCE * (1 - fraction)}
          />
          <circle cx="50" cy="50" r="37.5" fill="none" stroke="rgba(243,215,126,0.35)" strokeWidth="0.8" />
        </svg>
        <span
          data-testid="countdown"
          aria-hidden="true"
          className={`relative font-display text-[clamp(2.2rem,min(3.3vw,5.4vh),3.8rem)] font-bold leading-none tabular-nums max-sm:text-[1.7rem] ${urgent ? 'text-[#ff9aa2]' : 'text-[#fbf6ec]'}`}
        >
          {seconds}
        </span>
      </div>
      <span className="sr-only" aria-live="polite" data-testid="timer-announcement">
        {warning ? 'Son saniyələr' : ''}
      </span>
    </div>
  )
}
