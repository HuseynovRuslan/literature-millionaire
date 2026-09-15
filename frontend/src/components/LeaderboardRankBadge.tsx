import { Octagram } from './national/Ornaments'
import { formatRank } from '../utils/leaderboard'

export default function LeaderboardRankBadge({ rank, compact = false }: { rank: number; compact?: boolean }) {
  const label = `${formatRank(rank)} yer`
  const size = compact ? 'h-8 min-w-12 text-sm max-sm:whitespace-nowrap max-sm:px-1.5 max-sm:text-xs' : 'h-10 min-w-16 text-base'

  if (rank === 1) {
    return (
      <span aria-label={label} className={`inline-flex ${size} items-center justify-center gap-1 rounded-full border border-[var(--p-gold)] bg-[var(--p-paper)] px-2 font-semibold text-[var(--p-burgundy)]`}>
        <Octagram className={compact ? 'h-5 w-5' : 'h-6 w-6'} inner={false} />
        <span aria-hidden>{formatRank(rank)}</span>
      </span>
    )
  }

  if (rank === 2 || rank === 3) {
    return (
      <span aria-label={label} className={`inline-flex ${size} items-center justify-center rounded-full border border-[var(--p-gold-light)] bg-white px-2 font-semibold text-[var(--p-indigo)]`}>
        <span aria-hidden>{rank === 2 ? 'II' : 'III'} · {formatRank(rank)}</span>
      </span>
    )
  }

  return <span aria-label={label} className={`inline-flex ${size} items-center justify-center tabular-nums text-[var(--p-ink-2)]`}>{formatRank(rank)}</span>
}
