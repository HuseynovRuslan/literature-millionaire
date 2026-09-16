import { useId } from 'react'

/** Points of an eight-pointed star (two overlapping squares) around (cx, cy) with outer radius r. */
function starPoints(cx: number, cy: number, r: number): string {
  const inner = r * (Math.SQRT1_2 / Math.cos(Math.PI / 8)) // concave corners where the two squares cross
  const points: string[] = []
  for (let i = 0; i < 16; i++) {
    const radius = i % 2 === 0 ? r : inner
    const angle = -Math.PI / 2 + (i * Math.PI) / 8
    points.push(`${(cx + radius * Math.cos(angle)).toFixed(2)},${(cy + radius * Math.sin(angle)).toFixed(2)}`)
  }
  return points.join(' ')
}

/**
 * Neutral campaign emblem used when a campaign has no cover image: a gold eight-pointed star
 * with a burgundy medallion and a question mark. Drawn locally (no external or third-party asset),
 * so it suits any knowledge campaign.
 */
export function QuizEmblem({ className = '' }: { className?: string }) {
  const id = useId().replace(/:/g, '')
  return (
    <svg viewBox="0 0 240 240" className={className} aria-hidden="true" data-testid="quiz-emblem">
      <defs>
        <radialGradient id={`${id}-glow`}>
          <stop offset="0" stopColor="#f3d77e" stopOpacity="0.32" />
          <stop offset="1" stopColor="#f3d77e" stopOpacity="0" />
        </radialGradient>
        <linearGradient id={`${id}-gold`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#f7df93" />
          <stop offset="0.55" stopColor="#d4a83b" />
          <stop offset="1" stopColor="#9a6c21" />
        </linearGradient>
        <radialGradient id={`${id}-core`} cx="0.38" cy="0.32" r="0.8">
          <stop offset="0" stopColor="#c43d46" />
          <stop offset="0.6" stopColor="#7d161d" />
          <stop offset="1" stopColor="#4a0a13" />
        </radialGradient>
      </defs>
      <circle cx="120" cy="120" r="119" fill={`url(#${id}-glow)`} />
      <circle cx="120" cy="120" r="108" fill="none" stroke="#e9c069" strokeOpacity="0.45" strokeWidth="1.5" strokeDasharray="2 7" />
      <polygon points={starPoints(120, 120, 100)} fill={`url(#${id}-gold)`} stroke="#6f4d1c" strokeWidth="2" strokeLinejoin="round" />
      <polygon points={starPoints(120, 120, 84)} fill="none" stroke="#fdf0c2" strokeOpacity="0.65" strokeWidth="1.5" strokeLinejoin="round" />
      <circle cx="120" cy="120" r="58" fill={`url(#${id}-core)`} stroke="#f3d77e" strokeWidth="4" />
      <circle cx="120" cy="120" r="48" fill="none" stroke="#f3d77e" strokeOpacity="0.45" strokeWidth="1.2" />
      <path
        d="M101 104 C101 90 110 82 121 82 C133 82 142 90 142 101 C142 112 134 116 128 120 C123 123 121 127 121 133 L121 137"
        fill="none"
        stroke="#fbf6ec"
        strokeWidth="11"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="121" cy="155" r="6.5" fill="#fbf6ec" />
    </svg>
  )
}

/** Small reward medal: gold star medallion on burgundy ribbons. */
export function RewardMedal({ className = '' }: { className?: string }) {
  const id = useId().replace(/:/g, '')
  return (
    <svg viewBox="0 0 64 86" className={className} aria-hidden="true">
      <defs>
        <linearGradient id={`${id}-gold`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#f7df93" />
          <stop offset="0.6" stopColor="#d4a83b" />
          <stop offset="1" stopColor="#9a6c21" />
        </linearGradient>
      </defs>
      <path d="M20 44 L11 82 L23 75 L30 85 L35 48 Z" fill="#7d161d" />
      <path d="M44 44 L53 82 L41 75 L34 85 L29 48 Z" fill="#a82d36" />
      <polygon points={starPoints(32, 32, 30)} fill={`url(#${id}-gold)`} stroke="#6f4d1c" strokeWidth="1.2" strokeLinejoin="round" />
      <circle cx="32" cy="32" r="14" fill="#fbf6ec" stroke="#cf9c3c" strokeWidth="2" />
      <polygon points={starPoints(32, 32, 8.5)} fill="#7d161d" />
    </svg>
  )
}

/** "One attempt" badge: gold star medallion with the number 1. */
export function RuleBadge({ className = '' }: { className?: string }) {
  const id = useId().replace(/:/g, '')
  return (
    <svg viewBox="0 0 64 64" className={className} aria-hidden="true">
      <defs>
        <linearGradient id={`${id}-gold`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#f7df93" />
          <stop offset="0.6" stopColor="#d4a83b" />
          <stop offset="1" stopColor="#9a6c21" />
        </linearGradient>
      </defs>
      <polygon points={starPoints(32, 32, 30)} fill={`url(#${id}-gold)`} stroke="#6f4d1c" strokeWidth="1.2" strokeLinejoin="round" />
      <circle cx="32" cy="32" r="17" fill="#7d161d" stroke="#f3d77e" strokeWidth="2" />
      {/* Sans digit: the display serif draws an old-style "1" that reads like an "I" at this size. */}
      <text x="32" y="41" textAnchor="middle" fontFamily="'Fira Sans', 'Segoe UI', sans-serif" fontWeight="700" fontSize="24" fill="#fbf6ec">1</text>
    </svg>
  )
}

/** Passed result: gold eight-pointed star with a burgundy core and a small ivory star. */
export function VictoryStar({ className = '' }: { className?: string }) {
  const id = useId().replace(/:/g, '')
  return (
    <svg viewBox="0 0 120 120" className={className} aria-hidden="true" data-testid="victory-star">
      <defs>
        <radialGradient id={`${id}-glow`}>
          <stop offset="0" stopColor="#f3d77e" stopOpacity="0.35" />
          <stop offset="1" stopColor="#f3d77e" stopOpacity="0" />
        </radialGradient>
        <linearGradient id={`${id}-gold`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#f7df93" />
          <stop offset="0.55" stopColor="#d4a83b" />
          <stop offset="1" stopColor="#9a6c21" />
        </linearGradient>
      </defs>
      <circle cx="60" cy="60" r="59" fill={`url(#${id}-glow)`} />
      <polygon points={starPoints(60, 60, 50)} fill={`url(#${id}-gold)`} stroke="#6f4d1c" strokeWidth="1.5" strokeLinejoin="round" />
      <circle cx="60" cy="60" r="28" fill="#7d161d" stroke="#f3d77e" strokeWidth="2.5" />
      <polygon points={starPoints(60, 60, 16)} fill="#fbf6ec" />
    </svg>
  )
}

/** Failed result: a calm open book (encouragement to keep reading), drawn for the dark stage. */
export function OpenBookMark({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 120 80" className={className} aria-hidden="true" fill="none" stroke="#f3d77e" strokeWidth="3" strokeLinejoin="round" strokeLinecap="round">
      <path d="M60 22 C50 12 30 10 8 14 L8 66 C30 62 50 64 60 74 Z" fill="#fbf6ec" />
      <path d="M60 22 C70 12 90 10 112 14 L112 66 C90 62 70 64 60 74 Z" fill="#fbf6ec" />
      <path d="M60 22 L60 74" />
      <path d="M20 26 C32 24 44 25 52 30 M20 38 C32 36 44 37 52 42 M20 50 C32 48 44 49 52 54" stroke="#cf9c3c" strokeWidth="2.4" />
      <path d="M68 30 C76 25 88 24 100 26 M68 42 C76 37 88 36 100 38 M68 54 C76 49 88 48 100 50" stroke="#cf9c3c" strokeWidth="2.4" />
    </svg>
  )
}

/** Session expired: an hourglass inside a gold eight-pointed star frame. */
export function HourglassMark({ className = '' }: { className?: string }) {
  const id = useId().replace(/:/g, '')
  return (
    <svg viewBox="0 0 120 120" className={className} aria-hidden="true">
      <defs>
        <linearGradient id={`${id}-gold`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#f7df93" />
          <stop offset="0.6" stopColor="#d4a83b" />
          <stop offset="1" stopColor="#9a6c21" />
        </linearGradient>
      </defs>
      <polygon points={starPoints(60, 60, 56)} fill={`url(#${id}-gold)`} stroke="#6f4d1c" strokeWidth="1.5" strokeLinejoin="round" />
      <circle cx="60" cy="60" r="36" fill="#13274d" stroke="#f3d77e" strokeWidth="2.5" />
      <path d="M46 38 H74 M46 82 H74" stroke="#f3d77e" strokeWidth="4" strokeLinecap="round" />
      <path d="M49 40 C49 52 60 56 60 60 C60 64 49 68 49 80 H71 C71 68 60 64 60 60 C60 56 71 52 71 40 Z" fill="none" stroke="#fbf6ec" strokeWidth="3" strokeLinejoin="round" />
      <path d="M53 78 C55 71 60 69 60 69 C60 69 65 71 67 78 Z" fill="#f3d77e" />
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
