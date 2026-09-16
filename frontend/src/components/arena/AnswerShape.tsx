import type { AnswerOption } from '../../types/game'

/**
 * The shape that goes with each answer slot: triangle, diamond, circle, square. Shape plus letter means
 * an answer is never identified by colour alone (colour-blind players, glare on a kiosk screen).
 */
export default function AnswerShape({ option, className = '' }: { option: AnswerOption; className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="currentColor">
      {option === 'A' && <path d="M12 3.2 21.4 19.6a.9.9 0 0 1-.8 1.3H3.4a.9.9 0 0 1-.8-1.3Z" />}
      {option === 'B' && <path d="M12 2.2 21.8 12 12 21.8 2.2 12Z" />}
      {option === 'C' && <circle cx="12" cy="12" r="9.6" />}
      {option === 'D' && <rect x="3.2" y="3.2" width="17.6" height="17.6" rx="2" />}
    </svg>
  )
}

