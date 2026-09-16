import { useId } from 'react'
import AnswerShape from '../arena/AnswerShape'

/**
 * Illustrations for the quiz screens, drawn locally (no external assets). They share the arena palette:
 * violet brand, sunny gold for rewards, and the four answer shapes as the recurring motif.
 */

/** Campaign emblem: a glossy violet tile holding a question mark, orbited by the four answer shapes. */
export function QuizEmblem({ className = '' }: { className?: string }) {
  const id = useId().replace(/:/g, '')
  return (
    <div className={`relative ${className}`} data-testid="quiz-emblem" aria-hidden="true">
      <svg viewBox="0 0 120 120" className="absolute inset-0 size-full">
        <defs>
          <linearGradient id={`${id}-tile`} x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stopColor="#9d86ff" />
            <stop offset="1" stopColor="#5a3fe0" />
          </linearGradient>
        </defs>
        <rect x="22" y="26" width="76" height="76" rx="22" fill="#2a1d7a" />
        <rect x="22" y="20" width="76" height="76" rx="22" fill={`url(#${id}-tile)`} />
        <rect x="30" y="26" width="60" height="18" rx="9" fill="#ffffff" opacity="0.14" />
        <path d="M49 50c0-7 5-12 11.5-12S72 42.5 72 49c0 6-4.5 8-7.5 10-2.5 1.6-3.5 3.3-3.5 6.5" fill="none" stroke="#ffffff" strokeWidth="7" strokeLinecap="round" />
        <circle cx="61" cy="78" r="4.6" fill="#ffc93d" />
      </svg>
      <AnswerShape option="A" className="absolute left-[2%] top-[4%] size-[20%] text-opt-a" />
      <AnswerShape option="B" className="absolute right-0 top-[14%] size-[18%] text-brand-soft" />
      <AnswerShape option="C" className="absolute bottom-[2%] left-[6%] size-[16%] text-sun" />
      <AnswerShape option="D" className="absolute bottom-[8%] right-[4%] size-[16%] text-opt-d" />
    </div>
  )
}

/** Reward: a small gift box in gold and violet. */
export function RewardMedal({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 64 64" className={className} aria-hidden="true">
      <rect x="8" y="26" width="48" height="32" rx="6" fill="#ffc93d" />
      <rect x="8" y="30" width="48" height="5" fill="#d99a00" opacity="0.5" />
      <rect x="5" y="18" width="54" height="12" rx="4" fill="#ffe08a" />
      <rect x="28" y="18" width="8" height="40" fill="#7b61ff" />
      <path d="M32 18c-4-9-15-11-15-4 0 5 9 4 15 4Zm0 0c4-9 15-11 15-4 0 5-9 4-15 4Z" fill="#7b61ff" />
    </svg>
  )
}

/** "One attempt" badge: a gold coin with the number 1. */
export function RuleBadge({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 64 64" className={className} aria-hidden="true">
      <circle cx="32" cy="34" r="27" fill="#d99a00" />
      <circle cx="32" cy="30" r="27" fill="#ffc93d" />
      <circle cx="32" cy="30" r="20" fill="none" stroke="#fff3c4" strokeWidth="2" opacity="0.8" />
      <text x="32" y="39" textAnchor="middle" fontFamily="Unbounded, Manrope, sans-serif" fontWeight="800" fontSize="24" fill="#3a2600">1</text>
    </svg>
  )
}

/** Passed: a gold trophy with a star. */
export function VictoryStar({ className = '' }: { className?: string }) {
  const id = useId().replace(/:/g, '')
  return (
    <svg viewBox="0 0 120 120" className={className} aria-hidden="true" data-testid="victory-star">
      <defs>
        <radialGradient id={`${id}-glow`}>
          <stop offset="0" stopColor="#ffc93d" stopOpacity="0.45" />
          <stop offset="1" stopColor="#ffc93d" stopOpacity="0" />
        </radialGradient>
        <linearGradient id={`${id}-gold`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#ffe89a" />
          <stop offset="1" stopColor="#f0a800" />
        </linearGradient>
      </defs>
      <circle cx="60" cy="56" r="58" fill={`url(#${id}-glow)`} />
      <path d="M36 30c-14 0-18 22 4 30M84 30c14 0 18 22-4 30" fill="none" stroke="#f0a800" strokeWidth="7" strokeLinecap="round" />
      <path d="M34 22h52v18c0 17-11.5 30-26 30S34 57 34 40Z" fill={`url(#${id}-gold)`} />
      <rect x="54" y="68" width="12" height="16" fill="#d99a00" />
      <rect x="40" y="84" width="40" height="12" rx="4" fill="#7b61ff" />
      <path d="m60 32 4.4 8.9 9.8 1.4-7.1 6.9 1.7 9.8L60 54.4l-8.8 4.6 1.7-9.8-7.1-6.9 9.8-1.4Z" fill="#ffffff" />
    </svg>
  )
}

/** Failed: an open book — encouragement to keep reading. */
export function OpenBookMark({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 120 84" className={className} aria-hidden="true" strokeLinejoin="round" strokeLinecap="round">
      <path d="M60 20C50 10 30 8 8 12v56c22-4 42-2 52 8Z" fill="#f7f5ff" stroke="#b3a3ff" strokeWidth="3" />
      <path d="M60 20c10-10 30-12 52-8v56c-22-4-42-2-52 8Z" fill="#e6e0ff" stroke="#b3a3ff" strokeWidth="3" />
      <path d="M20 26c12-2 24-1 32 4M20 38c12-2 24-1 32 4M20 50c12-2 24-1 32 4M68 30c8-5 20-6 32-4M68 42c8-5 20-6 32-4M68 54c8-5 20-6 32-4" fill="none" stroke="#7b61ff" strokeWidth="2.6" opacity="0.7" />
    </svg>
  )
}

/** Session expired: an hourglass on a violet tile. */
export function HourglassMark({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 120 120" className={className} aria-hidden="true">
      <rect x="18" y="24" width="84" height="84" rx="26" fill="#2a1d7a" />
      <rect x="18" y="16" width="84" height="84" rx="26" fill="#6b50f0" />
      <path d="M42 36h36M42 80h36" stroke="#ffffff" strokeWidth="6" strokeLinecap="round" />
      <path d="M46 38c0 13 14 16 14 20s-14 7-14 20h28c0-13-14-16-14-20s14-7 14-20Z" fill="none" stroke="#ffffff" strokeWidth="4" strokeLinejoin="round" />
      <path d="M51 76c2-6 9-9 9-9s7 3 9 9Z" fill="#ffc93d" />
    </svg>
  )
}

export function PlayIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true">
      <path d="M7.5 4.8v14.4a.8.8 0 0 0 1.2.7l11.3-7.2a.8.8 0 0 0 0-1.4L8.7 4.1a.8.8 0 0 0-1.2.7z" fill="currentColor" />
    </svg>
  )
}

/* Small inline icons for chips, headings and buttons. */

export function ClockIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3 2" />
    </svg>
  )
}

export function TargetIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2">
      <circle cx="12" cy="12" r="9" />
      <circle cx="12" cy="12" r="5" />
      <circle cx="12" cy="12" r="1.4" fill="currentColor" />
    </svg>
  )
}

export function ListIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
      <path d="M9 6h11M9 12h11M9 18h11" />
      <circle cx="4.5" cy="6" r="1" fill="currentColor" />
      <circle cx="4.5" cy="12" r="1" fill="currentColor" />
      <circle cx="4.5" cy="18" r="1" fill="currentColor" />
    </svg>
  )
}

export function TrophyIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M7 4h10v5a5 5 0 0 1-10 0Z" />
      <path d="M7 6H4.5a2.5 2.5 0 0 0 2.8 3.9M17 6h2.5a2.5 2.5 0 0 1-2.8 3.9M12 14v4M8 20h8" />
    </svg>
  )
}

export function UserIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 20c1.5-4 4.5-6 8-6s6.5 2 8 6" />
    </svg>
  )
}

export function PhoneIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="6.5" y="2.5" width="11" height="19" rx="2.5" />
      <path d="M10.5 18.5h3" />
    </svg>
  )
}

export function CheckIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
      <path d="m5 12.5 4.5 4.5L19 7.5" />
    </svg>
  )
}

export function LockIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round">
      <rect x="5" y="10.5" width="14" height="10" rx="2.5" />
      <path d="M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5" />
    </svg>
  )
}

export function ArrowLeftIcon({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
      <path d="M15 5l-7 7 7 7" />
    </svg>
  )
}
