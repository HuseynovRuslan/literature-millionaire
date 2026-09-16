import { useId } from 'react'
import { formatRank } from '../../utils/leaderboard'

const MEDALS = {
  1: { key: 'gold', light: '#f7df93', dark: '#b8862a' },
  2: { key: 'silver', light: '#f2f4f8', dark: '#9aa5b5' },
  3: { key: 'bronze', light: '#f1c39a', dark: '#a8683a' },
} as const

/**
 * Rank marker on the dark game-show stage: gold, silver and bronze star medals for the first three places,
 * a plain numbered ring for the rest. The rank itself comes from the server; this only renders it.
 */
export default function RankMedal({ rank, compact = false }: { rank: number; compact?: boolean }) {
  const id = useId().replace(/:/g, '')
  const label = `${formatRank(rank)} yer`
  const size = compact ? 'size-[clamp(1.8rem,3vw,2.7rem)] max-sm:size-8' : 'size-[clamp(2.5rem,3.4vw,3.06rem)] max-sm:size-8'
  const medal = MEDALS[rank as 1 | 2 | 3]

  if (!medal) {
    return (
      <span role="img" aria-label={label} data-medal="none" className={`grid shrink-0 place-items-center rounded-full ring-1 ring-white/30 ${size}`}>
        <span aria-hidden className="font-display text-[clamp(1.05rem,1.4vw,1.26rem)] font-bold leading-none tabular-nums text-[#e4e9f3] max-sm:text-[0.95rem]">{rank}</span>
      </span>
    )
  }

  return (
    <span role="img" aria-label={label} data-medal={medal.key} className={`relative grid shrink-0 place-items-center ${size}`}>
      <svg viewBox="0 0 40 40" className="absolute inset-0 size-full" aria-hidden="true">
        <defs>
          <linearGradient id={`${id}-m`} x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stopColor={medal.light} />
            <stop offset="1" stopColor={medal.dark} />
          </linearGradient>
        </defs>
        <rect x="8" y="8" width="24" height="24" rx="1.5" fill={`url(#${id}-m)`} stroke="rgba(0,0,0,0.25)" strokeWidth="0.8" />
        <rect x="8" y="8" width="24" height="24" rx="1.5" transform="rotate(45 20 20)" fill={`url(#${id}-m)`} stroke="rgba(0,0,0,0.25)" strokeWidth="0.8" />
        <circle cx="20" cy="20" r="10.5" fill="none" stroke="rgba(255,255,255,0.55)" strokeWidth="0.8" />
      </svg>
      <span aria-hidden className="relative font-display text-[clamp(1.05rem,1.4vw,1.26rem)] font-bold leading-none tabular-nums text-[#2a1c14] max-sm:text-[0.95rem]">{rank}</span>
    </span>
  )
}
