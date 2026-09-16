import { formatRank } from '../../utils/leaderboard'

const MEDALS = {
  1: { key: 'gold', className: 'bg-[linear-gradient(180deg,#ffe38a,#f0b400)] shadow-[0_3px_0_#b88400] text-[#3a2600]' },
  2: { key: 'silver', className: 'bg-[linear-gradient(180deg,#f4f6fb,#b9c2d3)] shadow-[0_3px_0_#8b95a8] text-[#22283a]' },
  3: { key: 'bronze', className: 'bg-[linear-gradient(180deg,#f6c39a,#c97a45)] shadow-[0_3px_0_#935331] text-[#3a1d0b]' },
} as const

/**
 * Rank marker: gold, silver and bronze coins for the first three places, a plain numbered ring for the rest.
 * The rank itself comes from the server; this only renders it.
 */
export default function RankMedal({ rank, compact = false }: { rank: number; compact?: boolean }) {
  const label = `${formatRank(rank)} yer`
  const size = compact ? 'size-[clamp(2rem,2.8vw,2.6rem)] max-sm:size-8' : 'size-[clamp(2.6rem,3.4vw,3.2rem)] max-sm:size-9'
  const medal = MEDALS[rank as 1 | 2 | 3]
  const number = 'font-display text-[clamp(0.9rem,1.1vw,1.05rem)] font-extrabold leading-none tabular-nums max-sm:text-[0.85rem]'

  if (!medal) {
    return (
      <span role="img" aria-label={label} data-medal="none" className={`grid shrink-0 place-items-center rounded-full bg-white/[0.06] text-fg-2 ring-1 ring-white/20 ${size}`}>
        <span aria-hidden className={number}>{rank}</span>
      </span>
    )
  }

  return (
    <span role="img" aria-label={label} data-medal={medal.key} className={`grid shrink-0 place-items-center rounded-full ${medal.className} ${size}`}>
      <span aria-hidden className={number}>{rank}</span>
    </span>
  )
}
