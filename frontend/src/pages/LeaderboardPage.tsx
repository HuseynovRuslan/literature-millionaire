import { startTransition, useRef, useState, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PlayIcon, TrophyIcon } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import RankMedal from '../components/home/RankMedal'
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

/** Centred message block inside the stage (loading, empty, error, not found). */
function StageMessage({ children, role }: { children: ReactNode; role?: 'status' | 'alert' }) {
  return (
    <div role={role} aria-live={role === 'status' ? 'polite' : undefined} className="flex min-h-[20rem] flex-col items-center justify-center px-6 text-center max-sm:min-h-[15rem] max-sm:px-2">
      {children}
    </div>
  )
}

const MESSAGE_TITLE = 'font-display text-[clamp(1.5rem,2.6vw,2.3rem)] font-bold max-sm:text-[1.35rem]'
const MESSAGE_TEXT = 'mt-2 max-w-[40rem] text-[clamp(1rem,1.25vw,1.15rem)] font-medium text-fg-2 max-sm:text-[0.95rem]'

/** Podium heights and colours for places 1-3, shown in the classic 2-1-3 order. */
const PODIUM = {
  1: { height: 'h-[clamp(7rem,14vh,9.5rem)] max-sm:h-24', bar: 'bg-[linear-gradient(180deg,#ffd75e,#e0a100)] text-[#3a2600]' },
  2: { height: 'h-[clamp(5rem,10vh,7rem)] max-sm:h-16', bar: 'bg-[linear-gradient(180deg,#e3e8f2,#9aa5b8)] text-[#22283a]' },
  3: { height: 'h-[clamp(3.8rem,7.5vh,5.5rem)] max-sm:h-12', bar: 'bg-[linear-gradient(180deg,#f0b88c,#b36a3a)] text-[#3a1d0b]' },
} as const

function Podium({ entries }: { entries: LeaderboardEntry[] }) {
  const byRank = (rank: number) => entries.find((e) => e.rank === rank)
  const order = [2, 1, 3].map(byRank).filter((e): e is LeaderboardEntry => !!e && e.rank <= 3)
  if (order.length === 0) return null
  return (
    <div className="mx-auto grid w-full max-w-[44rem] grid-cols-3 items-end gap-[clamp(0.6rem,1.4vw,1.2rem)] px-2" aria-hidden="true">
      {order.map((entry) => {
        const p = PODIUM[entry.rank as 1 | 2 | 3]
        return (
          <div key={entry.rank} className={`pop flex min-w-0 flex-col items-center ${entry.rank === 1 ? 'col-start-2 row-start-1' : entry.rank === 2 ? 'col-start-1 row-start-1' : 'col-start-3 row-start-1'}`} style={{ animationDelay: `${(4 - entry.rank) * 120}ms` }}>
            {entry.rank === 1 && <TrophyIcon className="mb-1 size-8 text-sun max-sm:size-6" />}
            <p className="w-full truncate text-center text-[clamp(0.9rem,1.15vw,1.1rem)] font-extrabold text-fg max-sm:text-[0.8rem]">{entry.displayName}</p>
            <p className="font-display text-[clamp(0.9rem,1.1vw,1.05rem)] font-bold tabular-nums text-sun max-sm:text-[0.78rem]">{entry.pointsEarned} xal</p>
            <div className={`mt-2 flex w-full items-start justify-center rounded-t-2xl pt-2 font-display text-[clamp(1.6rem,2.6vw,2.4rem)] font-extrabold shadow-[inset_0_2px_0_rgba(255,255,255,0.45)] ${p.height} ${p.bar} max-sm:text-xl`}>
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
  const { load, retry } = useLeaderboard(campaignId, 10)
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

  return (
    <GameShowShell>
      <section
        data-testid="leaderboard-stage"
        aria-labelledby="leaderboard-title"
        // Masked names are short by design ("Sinaq Y."), so the panel keeps a readable width on wide monitors.
        className="card rise mx-auto flex w-full min-h-0 max-w-[64rem] flex-col rounded-[2rem] px-[clamp(1rem,2.8vw,2.6rem)] py-[clamp(1.2rem,2.6vh,2rem)] max-sm:rounded-3xl max-sm:px-3 max-sm:py-5"
      >
        <header className="flex flex-col items-center text-center">
          <p className="chip px-4 py-1.5 text-[clamp(0.75rem,0.9vw,0.88rem)] uppercase tracking-[0.14em] text-brand-soft max-sm:text-[0.7rem]">
            {!invalidRoute && load.kind === 'ready' ? load.data.quizMode.title : 'Kampaniya nəticələri'}
          </p>
          <h1 id="leaderboard-title" className="text-gradient mt-3 font-display text-[clamp(2rem,3.6vw,3.2rem)] font-extrabold leading-tight max-sm:text-[1.7rem]">Lider cədvəli</h1>
          <p className="text-[clamp(0.85rem,1vw,0.95rem)] font-bold text-fg-3 max-sm:text-[0.8rem]">Top 10</p>
        </header>

        <div className="mt-5">
          {invalidRoute && (
            <StageMessage role="alert">
              <p className={MESSAGE_TITLE}>Kampaniya ünvanı düzgün deyil</p>
              <p className={MESSAGE_TEXT}>Lider cədvəlini açmaq üçün etibarlı kampaniya seçin.</p>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'loading' && (
            <StageMessage role="status">
              <span className="spin inline-block size-12 rounded-full border-4 border-white/15 border-t-sun" aria-hidden />
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
              <TrophyIcon className="size-14 text-fg-3" />
              <p className={`${MESSAGE_TITLE} mt-3`}>Hələ tamamlanmış nəticə yoxdur</p>
              <p className={MESSAGE_TEXT}>İlk tamamlanmış quiz nəticəsi burada görünəcək.</p>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'ready' && load.data.entries.length > 0 && (
            <>
              <Podium entries={load.data.entries} />
              <table
                className="mt-4 w-full table-fixed border-separate border-spacing-y-2 text-left text-[clamp(0.95rem,1.15vw,1.1rem)] max-sm:text-[0.82rem] max-sm:[&_td]:px-1.5 max-sm:[&_th]:px-1.5"
                data-testid="leaderboard-table"
              >
                <caption className="sr-only">Kampaniyanın ilk on iştirakçısı və nəticələri</caption>
                <thead>
                  <tr className="text-[clamp(0.72rem,0.85vw,0.8rem)] uppercase tracking-[0.1em] text-fg-3 max-sm:text-[0.62rem] max-sm:tracking-[0.02em]">
                    <th scope="col" className="w-[12%] px-4 py-1 font-bold max-sm:w-[16%]">Yer</th>
                    <th scope="col" className="w-[38%] px-4 py-1 font-bold max-sm:w-[32%]">İştirakçı</th>
                    <th scope="col" className="w-[16%] px-4 py-1 text-right font-bold">Xal</th>
                    <th scope="col" className="w-[18%] px-4 py-1 text-right font-bold max-sm:w-[19%]">Düzgün cavab</th>
                    <th scope="col" className="w-[16%] px-4 py-1 text-right font-bold max-sm:w-[17%]">Müddət</th>
                  </tr>
                </thead>
                <tbody>
                  {load.data.entries.map((entry, i) => (
                    <tr key={entry.rank} className={`rise ${entry.rank <= 3 ? 'bg-sun/[0.08]' : 'bg-white/[0.05]'}`} style={{ animationDelay: `${120 + i * 40}ms` }}>
                      <th scope="row" className="h-14 rounded-l-2xl px-4 py-1 max-sm:h-12">
                        <RankMedal rank={entry.rank} compact />
                      </th>
                      <td className="px-4 py-1 font-bold">
                        <span className="block [overflow-wrap:anywhere]" data-testid="participant-name">{entry.displayName}</span>
                      </td>
                      <td className="px-4 py-1 text-right font-display font-extrabold tabular-nums text-sun">{entry.pointsEarned}/{entry.maxPoints}</td>
                      <td className="px-4 py-1 text-right font-semibold tabular-nums">{entry.correctAnswers}/{entry.totalQuestions}</td>
                      <td className="rounded-r-2xl px-4 py-1 text-right font-semibold tabular-nums text-fg-3">{formatDuration(entry.durationSeconds)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>
      </section>

      <nav aria-label="Lider cədvəli seçimləri" data-testid="leaderboard-actions" className="rise mx-auto flex w-full max-w-[64rem] items-stretch gap-4 [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
        <button type="button" onClick={() => campaignId && go(`/register/${campaignId}`, true)} disabled={navigating || invalidRoute} className={`${PRIMARY_CTA} flex-[1.5]`} data-testid="leaderboard-next">
          Növbəti iştirakçı
          <PlayIcon className="size-[0.85em] shrink-0" />
        </button>
        <button type="button" onClick={() => go('/', false)} disabled={navigating} className={`${SECONDARY_CTA} flex-1`} data-testid="leaderboard-home">Ana səhifə</button>
      </nav>
    </GameShowShell>
  )
}
