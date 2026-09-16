import { startTransition, useRef, useState, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import RankMedal from '../components/home/RankMedal'
import { useGame } from '../game/GameContext'
import { useLeaderboard } from '../hooks/useLeaderboard'

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
    <div role={role} aria-live={role === 'status' ? 'polite' : undefined} className="flex min-h-[22.3125rem] flex-col items-center justify-center px-6 text-center max-sm:min-h-[16rem] max-sm:px-2">
      {children}
    </div>
  )
}

const MESSAGE_TITLE = 'font-display text-[clamp(1.8rem,3vw,2.7rem)] font-bold text-[#fbf6ec] max-sm:text-[1.6rem]'
const MESSAGE_TEXT = 'mt-2 max-w-[40rem] text-[clamp(1.05rem,1.35vw,1.215rem)] text-[#d6deec] max-sm:text-[0.98rem]'

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
        // Masked names are short by design ("Sinaq Y."), so stretching this to a 1920px monitor leaves a
        // corridor of empty space between the name and the numbers. The panel keeps a readable width.
        className="home-stage rise mx-auto flex w-full min-h-0 max-w-[76rem] flex-col rounded-[clamp(1.2rem,1.6vw,1.44rem)] px-[clamp(1.2rem,2.8vw,2.52rem)] py-[1.1688rem] max-sm:rounded-2xl max-sm:px-3 max-sm:py-4"
      >
        <header className="px-[clamp(0rem,0.6vw,0.54rem)]">
          <p className="text-[clamp(0.85rem,1vw,0.9rem)] font-semibold uppercase tracking-[0.16em] text-[var(--p-gold-light)] max-sm:text-[0.75rem]">
            {!invalidRoute && load.kind === 'ready' ? load.data.quizMode.title : 'KAMPANİYA NƏTİCƏLƏRİ'}
          </p>
          <h1 id="leaderboard-title" className="font-display text-[clamp(2.2rem,3.6vw,3.24rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[1.9rem]">Lider cədvəli</h1>
          <p className="text-[clamp(0.85rem,1vw,0.9rem)] font-semibold text-[#c9d3e6] max-sm:text-[0.78rem]">Top 10</p>
        </header>

        <div className="mt-[0.6375rem]">
          {invalidRoute && (
            <StageMessage role="alert">
              <p className={MESSAGE_TITLE}>Kampaniya ünvanı düzgün deyil</p>
              <p className={MESSAGE_TEXT}>Lider cədvəlini açmaq üçün etibarlı kampaniya seçin.</p>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'loading' && (
            <StageMessage role="status">
              <span className="spin inline-block h-12 w-12 rounded-full border-4 border-white/20 border-t-[var(--p-gold-light)]" aria-hidden />
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
              <button type="button" onClick={retry} className={`${SECONDARY_CTA} mt-5 min-h-[4.5rem]! px-10`}>Yenidən yoxla</button>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'ready' && load.data.entries.length === 0 && (
            <StageMessage>
              <p className={MESSAGE_TITLE}>Hələ tamamlanmış nəticə yoxdur</p>
              <p className={MESSAGE_TEXT}>İlk tamamlanmış quiz nəticəsi burada görünəcək.</p>
            </StageMessage>
          )}

          {!invalidRoute && load.kind === 'ready' && load.data.entries.length > 0 && (
            <table
              className="w-full table-fixed border-separate border-spacing-y-[0.2922rem] text-left text-[clamp(1rem,1.3vw,1.17rem)] text-[#fbf6ec] max-sm:text-[0.82rem] max-sm:[&_td]:px-1.5 max-sm:[&_th]:px-1.5"
              data-testid="leaderboard-table"
            >
              <caption className="sr-only">Kampaniyanın ilk on iştirakçısı və nəticələri</caption>
              <thead>
                <tr className="text-[clamp(0.78rem,0.9vw,0.81rem)] uppercase tracking-[0.1em] text-[var(--p-gold-light)] max-sm:text-[0.66rem] max-sm:tracking-[0.02em]">
                  <th scope="col" className="w-[12%] px-4 py-1 font-semibold max-sm:w-[17%]">Yer</th>
                  <th scope="col" className="w-[40%] px-4 py-1 font-semibold max-sm:w-[31%]">İştirakçı</th>
                  <th scope="col" className="w-[16%] px-4 py-1 text-right font-semibold max-sm:w-[16%]">Xal</th>
                  <th scope="col" className="w-[17%] px-4 py-1 text-right font-semibold max-sm:w-[19%]">Düzgün cavab</th>
                  <th scope="col" className="w-[15%] px-4 py-1 text-right font-semibold max-sm:w-[17%]">Müddət</th>
                </tr>
              </thead>
              <tbody>
                {load.data.entries.map((entry) => (
                  <tr key={entry.rank} className={entry.rank <= 3 ? 'bg-[rgba(243,215,126,0.1)]' : 'bg-white/[0.06]'}>
                    <th scope="row" className="h-[2.2313rem] rounded-l-xl px-4 py-0.5 max-sm:h-11">
                      <RankMedal rank={entry.rank} compact />
                    </th>
                    <td className="px-4 py-0.5 font-semibold">
                      <span className="block [overflow-wrap:anywhere]" data-testid="participant-name">{entry.displayName}</span>
                    </td>
                    <td className="px-4 py-0.5 text-right font-display font-bold tabular-nums text-[var(--p-gold-light)]">{entry.pointsEarned}/{entry.maxPoints}</td>
                    <td className="px-4 py-0.5 text-right tabular-nums">{entry.correctAnswers}/{entry.totalQuestions}</td>
                    <td className="rounded-r-xl px-4 py-0.5 text-right tabular-nums text-[#c9d3e6]">{formatDuration(entry.durationSeconds)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </section>

      <nav aria-label="Lider cədvəli seçimləri" data-testid="leaderboard-actions" className="rise flex w-full max-w-[76rem] items-stretch justify-center gap-[clamp(0.8rem,1.4vw,1.26rem)] self-center [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
        <button type="button" onClick={() => campaignId && go(`/register/${campaignId}`, true)} disabled={navigating || invalidRoute} className={`${PRIMARY_CTA} min-h-[5.0469rem]! flex-[1.5] text-[clamp(1.5rem,2.3vw,2.07rem)]! disabled:opacity-60 max-sm:min-h-[3.75rem]! max-sm:text-[1.35rem]!`} data-testid="leaderboard-next">NÖVBƏTİ İŞTİRAKÇI</button>
        <button type="button" onClick={() => go('/', false)} disabled={navigating} className={`${SECONDARY_CTA} min-h-[5.0469rem]! flex-1 disabled:opacity-60 max-sm:min-h-[3.25rem]!`} data-testid="leaderboard-home">ANA SƏHİFƏ</button>
      </nav>
    </GameShowShell>
  )
}
