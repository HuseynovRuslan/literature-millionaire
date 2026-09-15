import { startTransition, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import LeaderboardRankBadge from '../components/LeaderboardRankBadge'
import CarpetFrame from '../components/national/CarpetFrame'
import KioskBrand from '../components/national/KioskBrand'
import { Buta, ButaRule } from '../components/national/Ornaments'
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
    <main className="kiosk paper flex flex-col">
      <CarpetFrame />
      <div className="relative z-0 mx-auto flex min-h-0 w-full max-w-[112rem] flex-1 flex-col" style={{ padding: 'calc(var(--frame) + 0.7rem) calc(var(--frame) + 1.3rem)' }}>
        <header className="grid shrink-0 grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-4">
          <KioskBrand compact className="justify-self-start" />
          <div className="text-center">
            <div className="flex items-center justify-center gap-3">
              <Buta className="h-7 w-5" flip />
              <p className="font-display text-[clamp(1rem,1.5vw,1.5rem)] font-semibold tracking-[0.18em] text-[var(--p-burgundy)]">AYIN KİTABI</p>
              <Buta className="h-7 w-5" />
            </div>
            <h1 className="font-display text-[clamp(2.2rem,4.3vw,4.5rem)] font-bold leading-none text-[var(--p-indigo)]">Lider cədvəli</h1>
            <ButaRule className="mx-auto mt-2 w-full max-w-[30rem]" />
          </div>
          <span aria-hidden />
        </header>

        <section className="mt-3 flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl border-[3px] border-[var(--p-gold)] bg-white p-2 shadow-[var(--p-shadow)] outline outline-1 outline-offset-[-9px] outline-[var(--p-gold-light)]" aria-labelledby="leaderboard-table-title">
          <h2 id="leaderboard-table-title" className="sr-only">İlk on iştirakçı</h2>

          {invalidRoute && (
            <div role="alert" className="flex min-h-0 flex-1 flex-col items-center justify-center px-6 text-center">
              <p className="font-display text-[clamp(1.7rem,3vw,3rem)] font-bold text-[var(--p-indigo)]">Kampaniya ünvanı düzgün deyil</p>
              <p className="mt-2 text-[var(--p-ink-2)]">Lider cədvəlini açmaq üçün etibarlı kampaniya seçin.</p>
            </div>
          )}

          {!invalidRoute && load.kind === 'loading' && (
            <div role="status" aria-live="polite" className="flex min-h-0 flex-1 flex-col items-center justify-center gap-5">
              <span className="spin inline-block h-12 w-12 rounded-full border-4 border-[var(--p-gold-light)] border-t-[var(--p-burgundy)]" aria-hidden />
              <p className="font-display text-[clamp(1.4rem,2.4vw,2.4rem)] font-semibold text-[var(--p-burgundy)]">Lider cədvəli yüklənir…</p>
            </div>
          )}

          {!invalidRoute && load.kind === 'error' && load.notFound && (
            <div role="alert" className="flex min-h-0 flex-1 flex-col items-center justify-center px-6 text-center">
              <p className="font-display text-[clamp(1.7rem,3vw,3rem)] font-bold text-[var(--p-indigo)]">Kampaniya tapılmadı</p>
              <p className="mt-2 text-[var(--p-ink-2)]">Bu ünvanda lider cədvəli yoxdur. Ana səhifədən cari kampaniyanı açın.</p>
            </div>
          )}

          {!invalidRoute && load.kind === 'error' && !load.notFound && (
            <div role="alert" className="flex min-h-0 flex-1 flex-col items-center justify-center px-6 text-center">
              <p className="font-display text-[clamp(1.7rem,3vw,3rem)] font-bold text-[var(--p-indigo)]">Lider cədvəlini yükləmək mümkün olmadı</p>
              <p className="mt-2 text-[var(--p-ink-2)]">Şəbəkə bağlantısını yoxlayın və yenidən cəhd edin.</p>
              <button type="button" onClick={retry} className="tap paper-ghost mt-5 min-h-[4.5rem] rounded-full px-10 font-semibold">Yenidən yoxla</button>
            </div>
          )}

          {!invalidRoute && load.kind === 'ready' && load.data.entries.length === 0 && (
            <div className="flex min-h-0 flex-1 flex-col items-center justify-center px-6 text-center">
              <p className="font-display text-[clamp(1.7rem,3vw,3rem)] font-bold text-[var(--p-indigo)]">Hələ tamamlanmış nəticə yoxdur</p>
              <p className="mt-2 text-[var(--p-ink-2)]">İlk tamamlanmış quiz nəticəsi burada görünəcək.</p>
            </div>
          )}

          {!invalidRoute && load.kind === 'ready' && load.data.entries.length > 0 && (
            <table className="h-full w-full table-fixed border-separate border-spacing-y-1 text-left text-[clamp(0.78rem,1.15vw,1.1rem)]" data-testid="leaderboard-table">
              <caption className="sr-only">Kampaniyanın ilk on iştirakçısı və nəticələri</caption>
              <thead>
                <tr className="text-[var(--p-ink-2)]">
                  <th scope="col" className="w-[15%] px-3 py-1 font-semibold">Yer</th>
                  <th scope="col" className="w-[35%] px-3 py-1 font-semibold">İştirakçı</th>
                  <th scope="col" className="w-[17%] px-3 py-1 text-right font-semibold">Xal</th>
                  <th scope="col" className="w-[20%] px-3 py-1 text-right font-semibold">Düzgün cavab</th>
                  <th scope="col" className="w-[13%] px-3 py-1 text-right font-semibold">Müddət</th>
                </tr>
              </thead>
              <tbody>
                {load.data.entries.map((entry) => (
                  <tr key={entry.rank} className="bg-[var(--p-paper)] text-[var(--p-ink)]">
                    <th scope="row" className="rounded-l-xl px-3 py-1"><LeaderboardRankBadge rank={entry.rank} compact /></th>
                    <td className="min-w-0 px-3 py-1 font-semibold"><span className="block truncate" title={entry.displayName}>{entry.displayName}</span></td>
                    <td className="px-3 py-1 text-right font-semibold tabular-nums text-[var(--p-burgundy)]">{entry.pointsEarned}/{entry.maxPoints}</td>
                    <td className="px-3 py-1 text-right tabular-nums">{entry.correctAnswers}/{entry.totalQuestions}</td>
                    <td className="rounded-r-xl px-3 py-1 text-right font-medium tabular-nums text-[var(--p-indigo)]">{formatDuration(entry.durationSeconds)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        <nav aria-label="Lider cədvəli seçimləri" className="mx-auto mt-3 grid w-full max-w-[54rem] shrink-0 grid-cols-2 gap-4">
          <button type="button" onClick={() => go('/', false)} disabled={navigating} className="tap paper-ghost flex min-h-[4.5rem] items-center justify-center rounded-full px-6 font-display text-[clamp(1.1rem,1.8vw,1.8rem)] font-semibold disabled:opacity-60">ANA SƏHİFƏ</button>
          <button type="button" onClick={() => go('/register', true)} disabled={navigating} className="tap paper-cta flex min-h-[4.5rem] items-center justify-center rounded-full px-6 font-display text-[clamp(1.1rem,1.8vw,1.8rem)] font-bold tracking-[0.04em] disabled:opacity-60">YENİ OYUN</button>
        </nav>
      </div>
    </main>
  )
}
