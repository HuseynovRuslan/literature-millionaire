import { startTransition, useRef, useState, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PlayIcon, TrophyIcon } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import { Octagram } from '../components/arena/NationalMotifs'
import GameShowShell from '../components/home/GameShowShell'
import RankMedal from '../components/home/RankMedal'
import { CarpetBand } from '../components/arena/NationalMotifs'
import { useGame } from '../game/GameContext'
import { useLeaderboard } from '../hooks/useLeaderboard'
import type { LeaderboardEntry } from '../types/leaderboard'

function parseCampaignId(value: string | undefined): number | null {
  if (!value || !/^[1-9]\d*$/.test(value)) return null
  const parsed = Number(value)
  return Number.isSafeInteger(parsed) ? parsed : null
}

function formatDuration(seconds: number): string {
  const total = Number.isFinite(seconds) ? Math.max(0, Math.round(seconds)) : 0
  const minutes = Math.floor(total / 60)
  const remainder = total % 60
  return `${minutes.toString().padStart(2, '0')}:${remainder.toString().padStart(2, '0')}`
}

/** Centred message block (loading, empty, error, not found). */
function StageMessage({ children, role }: { children: ReactNode; role?: 'status' | 'alert' }) {
  return (
    <div role={role} aria-live={role === 'status' ? 'polite' : undefined} className="flex min-h-[18rem] flex-1 flex-col items-center justify-center px-6 text-center max-sm:min-h-[14rem] max-sm:px-2">
      {children}
    </div>
  )
}

const MESSAGE_TITLE = 'font-display text-[clamp(1.4rem,2.4vw,2.1rem)] font-bold max-sm:text-[1.3rem]'
const MESSAGE_TEXT = 'mt-2 max-w-[40rem] text-[clamp(1rem,1.2vw,1.12rem)] font-medium text-fg-2 max-sm:text-[0.95rem]'

/** Podium heights and colours for places 1-3, shown in the classic 2-1-3 order. Heights follow the window height. */
const PODIUM = {
  1: { height: 'h-[clamp(6rem,19vh,12rem)] max-lg:h-28 max-sm:h-20', bar: 'bg-[linear-gradient(180deg,#ffd75e,#e0a100)] text-[#3a2600]', delay: 360 },
  2: { height: 'h-[clamp(4.4rem,13vh,8.4rem)] max-lg:h-20 max-sm:h-14', bar: 'bg-[linear-gradient(180deg,#e3e8f2,#9aa5b8)] text-[#22283a]', delay: 200 },
  3: { height: 'h-[clamp(3.4rem,9vh,6rem)] max-lg:h-14 max-sm:h-10', bar: 'bg-[linear-gradient(180deg,#f0b88c,#b36a3a)] text-[#3a1d0b]', delay: 80 },
} as const

function Podium({ entries }: { entries: LeaderboardEntry[] }) {
  const byRank = (rank: number) => entries.find((e) => e.rank === rank)
  const order = [2, 1, 3].map(byRank).filter((e): e is LeaderboardEntry => !!e && e.rank <= 3)
  if (order.length === 0) return null
  return (
    <div className="grid w-full grid-cols-3 items-end gap-[clamp(0.5rem,1.2vw,1rem)]" aria-hidden="true">
      {order.map((entry) => {
        const p = PODIUM[entry.rank as 1 | 2 | 3]
        const column = entry.rank === 1 ? 'col-start-2' : entry.rank === 2 ? 'col-start-1' : 'col-start-3'
        return (
          <div key={entry.rank} className={`row-start-1 flex min-w-0 flex-col items-center ${column}`}>
            {entry.rank === 1 && (
              <span className="bounce-in mb-1" style={{ animationDelay: '900ms' }}>
                <TrophyIcon className="bob size-[clamp(1.8rem,3.2vh,2.4rem)] text-sun" />
              </span>
            )}
            <p className="rise w-full truncate text-center text-[clamp(0.85rem,1.05vw,1.05rem)] font-extrabold text-fg max-sm:text-[0.78rem]" style={{ animationDelay: `${p.delay + 500}ms` }}>
              {entry.displayName}
            </p>
            <p className="rise font-display text-[clamp(0.85rem,1vw,1rem)] font-bold tabular-nums text-sun max-sm:text-[0.75rem]" style={{ animationDelay: `${p.delay + 580}ms` }}>
              {entry.pointsEarned} xal
            </p>
            <div
              className={`grow-up mt-2 flex w-full items-start justify-center rounded-t-2xl pt-2 font-display text-[clamp(1.4rem,2.4vw,2.2rem)] font-extrabold shadow-[inset_0_2px_0_rgba(255,255,255,0.45)] ${p.height} ${p.bar} max-sm:text-xl`}
              style={{ animationDelay: `${p.delay}ms` }}
            >
              {entry.rank}
            </div>
          </div>
        )
      })}
    </div>
  )
}

export default function LeaderboardPage() {
  const { campaignId: routeCampaignId } = useParams()
  const campaignId = parseCampaignId(routeCampaignId)
  const { load, retry } = useLeaderboard(campaignId, 'all')
  const { reset } = useGame()
  const navigate = useNavigate()
  const navigationLocked = useRef(false)
  const [navigating, setNavigating] = useState(false)

  function go(path: string, clearGame: boolean) {
    if (navigationLocked.current) return
    navigationLocked.current = true
    setNavigating(true)
    startTransition(() => {
      if (clearGame) reset()
      navigate(path)
    })
  }

  const invalidRoute = campaignId === null
  const ready = !invalidRoute && load.kind === 'ready' && load.data.entries.length > 0

  const actions = (
    <nav aria-label="Lider cədvəli seçimləri" data-testid="leaderboard-actions" className="rise flex w-full items-stretch gap-3 [animation-delay:200ms] max-sm:flex-col">
      <button type="button" onClick={() => campaignId && go(`/register/${campaignId}`, true)} disabled={navigating || invalidRoute} className={`${PRIMARY_CTA} min-h-[clamp(3.8rem,8vh,5rem)]! flex-[1.5] whitespace-nowrap text-[clamp(0.95rem,1.25vw,1.35rem)]!`} data-testid="leaderboard-next">
        Növbəti iştirakçı
        <PlayIcon className="size-[0.85em] shrink-0" />
      </button>
      <button type="button" onClick={() => go('/', false)} disabled={navigating} className={`${SECONDARY_CTA} min-h-[clamp(3.8rem,8vh,5rem)]! flex-1 whitespace-nowrap`} data-testid="leaderboard-home">Ana səhifə</button>
    </nav>
  )

  return (
    <GameShowShell>
      <div
        data-testid="leaderboard-stage"
        aria-labelledby="leaderboard-title"
        role="region"
        // On a laptop/kiosk: title, podium and actions on the left, the list of everyone who finished on the right,
        // row heights following the window height. A long list scrolls with the page while the left column stays in
        // view. Narrower screens stack: title and podium, the actions, then the list.
        className="mx-auto grid w-full max-w-[100rem] grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)] items-start gap-[clamp(1rem,1.6vw,1.5rem)] max-lg:grid-cols-1"
      >
        <div className="flex min-w-0 flex-col gap-[clamp(1rem,1.6vw,1.5rem)] lg:sticky lg:top-[clamp(1rem,2.4vh,1.8rem)] lg:col-start-1 lg:row-start-1">
        <section className="card card-glow rise flex min-w-0 flex-col justify-between gap-[clamp(0.8rem,2vh,1.4rem)] rounded-[2rem] px-[clamp(1rem,2.2vw,2.2rem)] py-[clamp(1rem,2.4vh,1.8rem)] max-sm:rounded-3xl max-sm:px-3.5">
          <header className="flex flex-col items-center text-center">
            <p className="chip px-4 py-1.5 text-[clamp(0.72rem,0.88vw,0.86rem)] uppercase tracking-[0.14em] text-brand-soft max-sm:text-[0.68rem]">
              {!invalidRoute && load.kind === 'ready' ? load.data.quizMode.title : 'Kampaniya nəticələri'}
            </p>
            <h1 id="leaderboard-title" className="text-gradient shimmer mt-[clamp(0.4rem,1.2vh,0.9rem)] font-display text-[clamp(1.9rem,min(3.4vw,6vh),3.2rem)] font-extrabold leading-tight max-sm:text-[1.7rem]">
              Lider cədvəli
            </h1>
            <p className="text-[clamp(0.82rem,0.95vw,0.92rem)] font-bold text-fg-3" data-testid="leaderboard-count">
              {!invalidRoute && load.kind === 'ready' ? `${load.data.entries.length} iştirakçı` : 'Bütün iştirakçılar'}
            </p>
          </header>

          {ready && (
            <div>
              <Podium entries={load.data.entries} />
              <CarpetBand className="text-sun/50" />
            </div>
          )}
        </section>
        {actions}
        </div>

        <section className="card rise flex min-h-full min-w-0 flex-col justify-center self-stretch rounded-[2rem] lg:col-start-2 lg:row-start-1 px-[clamp(0.8rem,1.8vw,1.8rem)] py-[clamp(0.8rem,2vh,1.4rem)] [animation-delay:120ms] max-sm:rounded-3xl max-sm:px-2.5">
          {invalidRoute && (
            <StageMessage role="alert">
              <p className={MESSAGE_TITLE}>Kampaniya ünvanı düzgün deyil</p>
              <p className={MESSAGE_TEXT}>Lider cədvəlini açmaq üçün etibarlı kampaniya seçin.</p>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'loading' && (
            <StageMessage role="status">
              <Octagram className="spin size-12 text-sun" />
              <p className={`${MESSAGE_TITLE} mt-5`}>Lider cədvəli yüklənir…</p>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'error' && load.notFound && (
            <StageMessage role="alert">
              <p className={MESSAGE_TITLE}>Kampaniya tapılmadı</p>
              <p className={MESSAGE_TEXT}>Bu ünvanda lider cədvəli yoxdur. Ana səhifədən cari kampaniyanı açın.</p>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'error' && !load.notFound && (
            <StageMessage role="alert">
              <p className={MESSAGE_TITLE}>Lider cədvəlini yükləmək mümkün olmadı</p>
              <p className={MESSAGE_TEXT}>Şəbəkə bağlantısını yoxlayın və yenidən cəhd edin.</p>
              <button type="button" onClick={retry} className={`${SECONDARY_CTA} mt-5 min-h-[4rem]! px-10`}>Yenidən yoxla</button>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'ready' && load.data.entries.length === 0 && (
            <StageMessage>
              <TrophyIcon className="bob size-14 text-fg-3" />
              <p className={`${MESSAGE_TITLE} mt-3`}>Hələ tamamlanmış nəticə yoxdur</p>
              <p className={MESSAGE_TEXT}>İlk tamamlanmış quiz nəticəsi burada görünəcək.</p>
            </StageMessage>
          )}

          {ready && (
            <table
              className="w-full table-fixed border-separate border-spacing-y-[clamp(0.2rem,0.6vh,0.4rem)] text-left text-[clamp(0.9rem,min(1.1vw,2vh),1.08rem)] max-sm:text-[0.8rem] max-sm:[&_td]:px-1.5 max-sm:[&_th]:px-1.5"
              data-testid="leaderboard-table"
            >
              <caption className="sr-only">Kampaniyanın bütün iştirakçıları və nəticələri</caption>
              <thead>
                <tr className="text-[clamp(0.68rem,0.8vw,0.78rem)] uppercase tracking-[0.1em] text-fg-3 max-sm:text-[0.6rem] max-sm:tracking-[0.02em]">
                  <th scope="col" className="w-[13%] px-3 py-1 font-bold max-sm:w-[16%]">Yer</th>
                  <th scope="col" className="w-[35%] px-3 py-1 font-bold max-sm:w-[31%]">İştirakçı</th>
                  <th scope="col" className="w-[16%] px-3 py-1 text-right font-bold">Xal</th>
                  <th scope="col" className="w-[19%] px-3 py-1 text-right font-bold max-sm:w-[20%]">Düzgün</th>
                  <th scope="col" className="w-[17%] px-3 py-1 text-right font-bold">Müddət</th>
                </tr>
              </thead>
              <tbody>
                {load.data.entries.map((entry, i) => (
                  // The first rows arrive one by one; the rest of a long list comes in together, not a minute later.
                  <tr key={entry.rank} className={`slide-in-right ${entry.rank <= 3 ? 'bg-sun/[0.08]' : 'bg-white/[0.05]'}`} style={{ animationDelay: `${200 + Math.min(i, 12) * 55}ms` }}>
                    <th scope="row" className="h-[clamp(2.4rem,5.6vh,3.5rem)] rounded-l-2xl px-3 py-0.5 max-sm:h-11">
                      <RankMedal rank={entry.rank} compact />
                    </th>
                    <td className="px-3 py-0.5 font-bold">
                      <span className="block truncate" data-testid="participant-name">{entry.displayName}</span>
                    </td>
                    <td className="px-3 py-0.5 text-right font-display font-extrabold tabular-nums text-sun">{entry.pointsEarned}/{entry.maxPoints}</td>
                    <td className="px-3 py-0.5 text-right font-semibold tabular-nums">{entry.correctAnswers}/{entry.totalQuestions}</td>
                    <td className="rounded-r-2xl px-3 py-0.5 text-right font-semibold tabular-nums text-fg-3">{formatDuration(entry.durationSeconds)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

      </div>
    </GameShowShell>
  )
}
