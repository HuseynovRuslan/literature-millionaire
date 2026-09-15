import { startTransition, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import LeaderboardRankBadge from '../components/LeaderboardRankBadge'
import BrandMark from '../components/national/BrandMark'
import CarpetFrame from '../components/national/CarpetFrame'
import { Buta, ButaRule, Octagram } from '../components/national/Ornaments'
import { useGame } from '../game/GameContext'
import { useLeaderboard, type LeaderboardLoad } from '../hooks/useLeaderboard'
import { formatRank } from '../utils/leaderboard'

/** Open book, outline only: the "read it again" symbol for a failed quiz. */
function OpenBook({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 120 80" className={className} aria-hidden="true" fill="none" stroke="var(--p-indigo)" strokeWidth="3" strokeLinejoin="round" strokeLinecap="round">
      <path d="M60 22 C50 12 30 10 8 14 L8 66 C30 62 50 64 60 74 Z" fill="#ffffff" />
      <path d="M60 22 C70 12 90 10 112 14 L112 66 C90 62 70 64 60 74 Z" fill="#ffffff" />
      <path d="M60 22 L60 74" />
      <path d="M20 26 C32 24 44 25 52 30 M20 38 C32 36 44 37 52 42 M20 50 C32 48 44 49 52 54" stroke="var(--p-gold)" strokeWidth="2.4" />
      <path d="M68 30 C76 25 88 24 100 26 M68 42 C76 37 88 36 100 38 M68 54 C76 49 88 48 100 50" stroke="var(--p-gold)" strokeWidth="2.4" />
    </svg>
  )
}

function TopFive({ load, retry }: { load: LeaderboardLoad; retry: () => void }) {
  return (
    <section className="relative flex min-h-0 flex-col rounded-xl border-[3px] border-[var(--p-gold)] bg-white px-[clamp(1rem,2.2vw,2rem)] py-[clamp(1rem,2vh,1.6rem)] shadow-[var(--p-shadow)] outline outline-1 outline-offset-[-9px] outline-[var(--p-gold-light)]" aria-labelledby="top-five-title">
      <div className="flex items-center justify-center gap-3">
        <Octagram className="h-9 w-9" inner={false} />
        <div className="text-center">
          <p className="text-xs font-semibold tracking-[0.16em] text-[var(--p-ink-2)]">KAMPANİYA NƏTİCƏLƏRİ</p>
          <h2 id="top-five-title" className="font-display text-[clamp(1.8rem,3vw,3rem)] font-bold leading-none text-[var(--p-burgundy)]">İlk beşlik</h2>
        </div>
      </div>
      <ButaRule className="my-[clamp(0.5rem,1.2vh,1rem)] w-full" />

      {load.kind === 'idle' && (
        <div className="flex min-h-0 flex-1 items-center justify-center text-center">
          <p className="font-display text-[clamp(1.35rem,2vw,2rem)] font-semibold text-[var(--p-indigo)]">Lider cədvəli hazırda əlçatan deyil</p>
        </div>
      )}

      {load.kind === 'loading' && (
        <div role="status" aria-live="polite" className="flex min-h-0 flex-1 flex-col justify-center gap-3">
          <p className="text-center font-medium text-[var(--p-ink-2)]">Lider cədvəli yüklənir…</p>
          {[1, 2, 3, 4, 5].map((row) => <span key={row} aria-hidden className="h-11 animate-pulse rounded-xl bg-[var(--p-paper-2)] motion-reduce:animate-none" />)}
        </div>
      )}

      {load.kind === 'error' && (
        <div role="alert" className="flex min-h-0 flex-1 flex-col items-center justify-center text-center">
          <p className="font-display text-[clamp(1.35rem,2vw,2rem)] font-semibold text-[var(--p-indigo)]">Lider cədvəlini yükləmək mümkün olmadı</p>
          <button type="button" onClick={retry} className="tap paper-ghost mt-5 min-h-[4.5rem] rounded-full px-8 font-semibold">Yenidən yoxla</button>
        </div>
      )}

      {load.kind === 'ready' && load.data.entries.length === 0 && (
        <div className="flex min-h-0 flex-1 items-center justify-center text-center">
          <p className="font-display text-[clamp(1.35rem,2vw,2rem)] font-semibold text-[var(--p-indigo)]">Hələ tamamlanmış nəticə yoxdur</p>
        </div>
      )}

      {load.kind === 'ready' && load.data.entries.length > 0 && (
        <ol className="flex min-h-0 flex-1 flex-col justify-center gap-[clamp(0.35rem,0.8vh,0.65rem)]" aria-label="İlk beş iştirakçı">
          {load.data.entries.map((entry) => (
            <li key={entry.rank} className="grid min-h-11 grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-xl border border-[var(--p-line)] bg-[var(--p-paper)] px-3 py-2">
              <LeaderboardRankBadge rank={entry.rank} compact />
              <span className="min-w-0 truncate font-semibold text-[var(--p-ink)]" title={entry.displayName}>{entry.displayName}</span>
              <span className="text-right text-sm tabular-nums text-[var(--p-ink-2)]">
                <strong className="block text-base text-[var(--p-burgundy)]">{entry.pointsEarned}/{entry.maxPoints} xal</strong>
                {entry.correctAnswers}/{entry.totalQuestions} düzgün
              </span>
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}

/** Final screen shown inside /game once the quiz has ended. All score and position values come from state.result. */
export default function GameResult() {
  const navigate = useNavigate()
  const { state, reset, error } = useGame()
  const r = state.result
  const campaignId = r && Number.isSafeInteger(r.campaignId) && r.campaignId > 0 ? r.campaignId : null
  const { load, retry } = useLeaderboard(campaignId, 5)
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

  const passed = r?.passed ?? false
  const title = passed ? 'Təbriklər!' : 'Bu dəfə alınmadı'
  const line = passed
    ? 'Siz "Ayın kitabı" viktorinasını uğurla keçdiniz.'
    : 'Kitabı bir daha oxuyun və yenidən cəhd edin.'

  return (
    <main className="kiosk paper flex flex-col" data-result={passed ? 'passed' : 'failed'}>
      <CarpetFrame />
      <div
        className="relative z-0 mx-auto flex min-h-0 w-full max-w-[112rem] flex-1 flex-col"
        style={{ padding: 'calc(var(--frame) + 0.65rem) calc(var(--frame) + 1.2rem)' }}
      >
        <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 lg:grid-cols-[minmax(0,0.9fr)_minmax(24rem,1.1fr)] lg:gap-5">
          <section className="rise relative flex min-h-0 flex-col items-center justify-center rounded-xl border-[3px] border-[var(--p-gold)] bg-white px-[clamp(1rem,2.4vw,2.4rem)] py-[clamp(0.8rem,1.8vh,1.6rem)] text-center shadow-[var(--p-shadow)] outline outline-1 outline-offset-[-9px] outline-[var(--p-gold-light)]">
            <Buta className="absolute -left-3 -top-4 z-10 h-12 w-9" flip />
            <Buta className="absolute -right-3 -top-4 z-10 h-12 w-9" />
            {passed ? <Octagram className="h-[clamp(3rem,5.5vh,4.5rem)] w-[clamp(3rem,5.5vh,4.5rem)]" /> : <OpenBook className="h-[clamp(2.8rem,5vh,4rem)] w-auto" />}
            <h1 className={`mt-1 font-display text-[clamp(2.1rem,4.2vw,4.4rem)] font-bold leading-none ${passed ? 'text-[var(--p-burgundy)]' : 'text-[var(--p-indigo)]'}`}>{title}</h1>
            <p className="mt-1 max-w-[40ch] text-[clamp(0.9rem,1.25vw,1.25rem)] leading-snug text-[var(--p-ink-2)]">{line}</p>
            <ButaRule className="my-[clamp(0.35rem,1vh,0.8rem)] w-full max-w-[24rem]" />

            {r && (
              <>
                <p className="text-sm font-medium text-[var(--p-ink-2)]">Düzgün cavab</p>
                <p className={`font-display text-[clamp(3rem,6.5vw,6rem)] font-bold leading-none tabular-nums ${passed ? 'text-[var(--p-burgundy)]' : 'text-[var(--p-indigo)]'}`} data-testid="score">
                  {r.correctAnswers}<span className="text-[0.48em] text-[var(--p-ink-2)]"> / {r.totalQuestions}</span>
                </p>
                <p className="mt-2 rounded-full border border-[var(--p-line)] bg-[var(--p-paper)] px-5 py-1.5 text-[clamp(0.95rem,1.35vw,1.3rem)] text-[var(--p-ink)]" data-testid="points">
                  Toplanan xal: <span className="font-semibold tabular-nums text-[var(--p-burgundy)]">{r.pointsEarned}</span> / {r.maxPoints}
                </p>
                <p className="mt-1.5 text-[clamp(0.82rem,1.05vw,1.05rem)] text-[var(--p-ink-2)]">Keçid üçün ən azı {r.passingScore} / {r.totalQuestions} düzgün cavab lazımdır.</p>
                <p className="mt-2 font-semibold text-[clamp(0.95rem,1.25vw,1.2rem)] text-[var(--p-indigo)]" data-testid="leaderboard-position">
                  {r.leaderboardPosition === null
                    ? 'Lider cədvəlində mövqe hazırda hesablanmadı'
                    : `Lider cədvəlində yeriniz: ${formatRank(r.leaderboardPosition)} yer`}
                </p>
                {passed && r.rewardTitle && (
                  <p className="mt-2 rounded-2xl border-2 border-[var(--p-gold)] bg-[var(--p-paper)] px-6 py-2 font-display text-[clamp(1.05rem,1.55vw,1.55rem)] font-semibold text-[var(--p-burgundy)]" data-testid="reward">Mükafat: {r.rewardTitle}</p>
                )}
              </>
            )}

            {error && <p role="alert" className="mt-2 rounded-xl border-2 border-[#b32a31] bg-white px-4 py-2 text-sm text-[var(--p-ink)]">{error}</p>}
          </section>

          <TopFive load={load} retry={retry} />
        </div>

        <nav aria-label="Nəticə seçimləri" className="mx-auto mt-4 grid w-full max-w-[76rem] grid-cols-1 gap-3 sm:grid-cols-3">
          <button type="button" onClick={() => go('/register', true)} disabled={navigating} className="tap paper-cta flex min-h-[4.5rem] items-center justify-center rounded-full px-5 font-display text-[clamp(1.1rem,1.8vw,1.8rem)] font-bold tracking-[0.03em] disabled:opacity-60">YENİDƏN OYNA</button>
          <button type="button" onClick={() => campaignId && go(`/leaderboard/${campaignId}`, false)} disabled={navigating || campaignId === null} className="tap paper-ghost flex min-h-[4.5rem] items-center justify-center rounded-full px-5 font-display text-[clamp(1.05rem,1.65vw,1.65rem)] font-bold tracking-[0.02em] disabled:opacity-60">TAM LİDER CƏDVƏLİ</button>
          <button type="button" onClick={() => go('/', true)} disabled={navigating} className="tap paper-ghost flex min-h-[4.5rem] items-center justify-center rounded-full px-5 font-display text-[clamp(1.1rem,1.8vw,1.8rem)] font-semibold disabled:opacity-60">ANA SƏHİFƏ</button>
        </nav>
      </div>

      <div className="absolute z-20 hidden xl:block" style={{ right: 'calc(var(--frame) + 1rem)', bottom: 'calc(var(--frame) + 0.7rem)' }}><BrandMark /></div>
    </main>
  )
}
