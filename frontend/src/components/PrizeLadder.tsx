import { formatPoints } from '../types/game'

interface Props {
  prizes: number[]
  /** 1-based number of the question being played. */
  current: number
  /** Highest question number answered correctly so far. */
  secured: number
}

/** Vertical 15-step ladder, top = final question. Narrow by design. */
export default function PrizeLadder({ prizes, current, secured }: Props) {
  const rows = prizes.map((p, i) => ({ number: i + 1, prize: p })).reverse()

  return (
    <aside
      aria-label="Mükafat pilləkəni"
      className="flex h-full flex-col justify-between rounded-2xl border border-navy-600/70 bg-navy-800/70 px-3 py-3"
    >
      {rows.map(({ number, prize }) => {
        const isCurrent = number === current
        const isSecured = number <= secured
        return (
          <div
            key={number}
            className={[
              'flex items-center justify-between rounded-lg px-3 font-medium leading-none transition-colors',
              'min-h-[2.6rem] text-[clamp(0.85rem,1.1vw,1.15rem)]',
              isCurrent
                ? 'bg-gold text-navy-900 shadow-[0_0_0_2px_rgba(232,210,156,0.6)]'
                : isSecured
                  ? 'text-gold-light'
                  : 'text-mist',
            ].join(' ')}
          >
            <span className={`w-6 font-display text-[1.15em] ${isCurrent ? 'text-navy-900' : 'text-gold/80'}`}>
              {number}
            </span>
            <span className="tabular-nums tracking-wide">{formatPoints(prize)}</span>
          </div>
        )
      })}
    </aside>
  )
}
