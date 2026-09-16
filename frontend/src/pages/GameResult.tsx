import { startTransition, useMemo, useRef, useState, type CSSProperties } from 'react'
import { useNavigate } from 'react-router-dom'
import { OpenBookMark, RewardMedal, TrophyIcon, VictoryStar } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import RankMedal from '../components/home/RankMedal'
import AnswerReview from '../components/game/AnswerReview'
import { useGame } from '../game/GameContext'
import { useLeaderboard, type LeaderboardLoad } from '../hooks/useLeaderboard'
import type { QuizAnswerReview } from '../types/game'
import { formatRank } from '../utils/leaderboard'

const CARD = 'card rise rounded-[2rem] max-sm:rounded-3xl'
const CONFETTI_COLORS = ['#ffc93d', '#7b61ff', '#c42f66', '#2c5fd8', '#3ddc97', '#ffffff']

/** A short burst of confetti for a passed quiz. Decorative only; hidden with reduced motion. */
function Confetti() {
  const pieces = useMemo(
    () =>
      Array.from({ length: 36 }, (_, i) => ({
        left: `${(i * 97) % 100}%`,
        delay: `${(i % 12) * 90}ms`,
        dx: `${((i * 53) % 160) - 80}px`,
        color: CONFETTI_COLORS[i % CONFETTI_COLORS.length],
      })),
    [],
  )
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden rounded-[inherit]" aria-hidden="true">
      {pieces.map((p, i) => (
        <span key={i} className="confetti" style={{ left: p.left, background: p.color, animationDelay: p.delay, '--dx': p.dx } as CSSProperties} />
      ))}
    </div>
  )
}

/** Score as a ring: correct answers against the total. */
function ScoreRing({ correct, total, passed }: { correct: number; total: number; passed: boolean }) {
  const r = 52
  const c = 2 * Math.PI * r
  const ratio = total > 0 ? correct / total : 0
  return (
    <div className="pop relative grid size-[clamp(10rem,16vw,13.5rem)] place-items-center [animation-delay:120ms] max-sm:size-40" data-testid="score">
      <svg viewBox="0 0 120 120" className="absolute inset-0 size-full -rotate-90" aria-hidden="true">
        <circle cx="60" cy="60" r={r} fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="11" />
        <circle cx="60" cy="60" r={r} fill="none" stroke={passed ? 'var(--color-sun)' : 'var(--color-brand-soft)'} strokeWidth="11" strokeLinecap="round" strokeDasharray={c} strokeDashoffset={c * (1 - ratio)} />
      </svg>
      <span className="relative text-center">
        <span className="block font-display text-[clamp(2.8rem,4.8vw,4.2rem)] font-extrabold leading-none tabular-nums">
          {correct}<span className="text-[0.45em] text-fg-3"> / {total}</span>
        </span>
        <span className="mt-1 block text-[clamp(0.7rem,0.85vw,0.82rem)] font-bold uppercase tracking-[0.14em] text-fg-3">Düzgün cavab</span>
      </span>
    </div>
  )
}

function TopFive({ load, retry }: { load: LeaderboardLoad; retry: () => void }) {
  return (
    <div className="flex min-h-[14rem] flex-1 flex-col justify-center max-sm:min-h-[10rem]">
      {load.kind === 'idle' && (
        <p className="text-center font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-bold text-fg-2">Lider cədvəli hazırda əlçatan deyil</p>
      )}

      {load.kind === 'loading' && (
        <div role="status" aria-live="polite" className="flex flex-col gap-2">
          <p className="text-center font-semibold text-fg-2">Lider cədvəli yüklənir…</p>
          {[1, 2, 3, 4, 5].map((row) => <span key={row} aria-hidden className="h-14 animate-pulse rounded-2xl bg-white/[0.06] motion-reduce:animate-none" />)}
        </div>
      )}

      {load.kind === 'error' && (
        <div role="alert" className="flex flex-col items-center text-center">
          <p className="font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-bold">Lider cədvəlini yükləmək mümkün olmadı</p>
          <button type="button" onClick={retry} className={`${SECONDARY_CTA} mt-4 min-h-[4rem]! px-10`}>Yenidən yoxla</button>
        </div>
      )}

      {load.kind === 'ready' && load.data.entries.length === 0 && (
        <p className="text-center font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-bold text-fg-2">Hələ tamamlanmış nəticə yoxdur</p>
      )}

      {load.kind === 'ready' && load.data.entries.length > 0 && (
        <ol className="flex flex-col gap-2" aria-label="İlk beş iştirakçı">
          {load.data.entries.map((entry) => (
            <li key={entry.rank} className={`grid min-h-14 grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-2xl px-3 py-2 ring-1 max-sm:gap-2.5 ${entry.rank === 1 ? 'bg-sun/10 ring-sun/30' : 'bg-white/[0.05] ring-white/10'}`}>
              <RankMedal rank={entry.rank} compact />
              <span className="min-w-0 text-[clamp(1rem,1.2vw,1.12rem)] font-bold text-fg [overflow-wrap:anywhere] max-sm:text-[0.95rem]">{entry.displayName}</span>
              <span className="text-right tabular-nums leading-tight">
                <strong className="block font-display text-[clamp(1.05rem,1.3vw,1.25rem)] font-extrabold text-sun max-sm:text-[1rem]">{entry.pointsEarned}/{entry.maxPoints} xal</strong>
                <span className="text-[clamp(0.8rem,0.9vw,0.86rem)] font-semibold text-fg-3 max-sm:text-[0.76rem]">{entry.correctAnswers}/{entry.totalQuestions} düzgün</span>
              </span>
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}

/**
 * Right-hand panel of the result screen. Two tabs share it: the answers open first, since that is the
 * only moment of the quiz when they are disclosed at all.
 */
function SidePanel({ load, retry, review }: { load: LeaderboardLoad; retry: () => void; review: QuizAnswerReview[] }) {
  const [tab, setTab] = useState<'answers' | 'top'>(review.length > 0 ? 'answers' : 'top')
  const tabs = [
    { id: 'answers' as const, label: 'Cavablarım', enabled: review.length > 0 },
    { id: 'top' as const, label: 'İlk beşlik', enabled: true },
  ].filter((t) => t.enabled)

  return (
    <section data-testid="top-five" className={`${CARD} flex min-h-0 flex-col px-[clamp(1rem,2vw,1.8rem)] py-5 [animation-delay:80ms] max-sm:px-3.5 max-sm:py-4`}>
      <p className="text-[clamp(0.75rem,0.88vw,0.85rem)] font-bold uppercase tracking-[0.16em] text-fg-3">Kampaniya nəticələri</p>

      <div role="tablist" aria-label="Nəticə paneli" className="mt-3 grid auto-cols-fr grid-flow-col gap-1 rounded-2xl bg-ink-950/60 p-1 ring-1 ring-white/10">
        {tabs.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            id={`panel-tab-${t.id}`}
            aria-selected={tab === t.id}
            aria-controls={`panel-${t.id}`}
            onClick={() => setTab(t.id)}
            data-testid={`panel-tab-${t.id}`}
            className={`tap min-h-12 rounded-xl px-4 font-display text-[clamp(0.95rem,1.2vw,1.15rem)] font-bold transition-colors ${
              tab === t.id ? 'bg-brand text-white shadow-[0_3px_0_#4a33c9]' : 'text-fg-2'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div
        role="tabpanel"
        id={`panel-${tab}`}
        aria-labelledby={`panel-tab-${tab}`}
        tabIndex={0}
        // The list scrolls inside its own panel rather than stretching the result screen, and the rail is
        // left visible because the screen is read on a touch device as often as with a mouse.
        // On a phone the cap is dropped: there the whole page scrolling is the natural gesture.
        className="mt-4 flex min-h-0 flex-1 flex-col overflow-y-auto overscroll-contain pr-1 [scrollbar-color:rgba(179,163,255,0.5)_transparent] [scrollbar-width:thin] sm:max-h-[min(54vh,34rem)] max-sm:overflow-visible max-sm:pr-0"
      >
        {tab === 'answers' ? <AnswerReview items={review} /> : <TopFive load={load} retry={retry} />}
      </div>
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
    ? 'Siz bilik yarışını uğurla keçdiniz.'
    : 'Növbəti bilik yarışında sizi yenidən gözləyirik.'
  // Only the final server result names the category and campaign: never the mid-game session state.
  const quizModeTitle = r?.quizMode.title ?? null

  return (
    <GameShowShell>
      <div data-result={passed ? 'passed' : 'failed'} className="grid min-h-0 grid-cols-[minmax(0,1fr)_minmax(0,1fr)] items-stretch gap-[clamp(1rem,1.8vw,1.6rem)] max-lg:grid-cols-1 max-sm:gap-4">
        <section
          data-testid="result-stage"
          aria-labelledby="result-title"
          className={`${CARD} ${passed ? 'card-glow' : ''} flex min-h-0 flex-col items-center justify-center px-[clamp(1.2rem,3vw,2.6rem)] py-[clamp(1.4rem,3vh,2.2rem)] text-center max-sm:px-4 max-sm:py-6`}
        >
          {passed && <Confetti />}
          <div className="flex items-center gap-4">
            {passed
              ? <VictoryStar className="pop size-[clamp(4rem,6.4vw,5.8rem)] max-sm:size-16" />
              : <OpenBookMark className="pop h-[clamp(3rem,5vw,4.2rem)] w-auto max-sm:h-12" />}
            <div className="text-left">
              {quizModeTitle && (
                <p data-testid="result-quiz-mode" className="text-[clamp(0.75rem,0.9vw,0.88rem)] font-bold uppercase tracking-[0.14em] text-brand-soft">
                  {quizModeTitle}
                </p>
              )}
              <h1 id="result-title" className={`mt-1 font-display text-[clamp(2rem,3.8vw,3.4rem)] font-extrabold leading-none max-sm:text-[1.8rem] ${passed ? 'text-gradient' : ''}`}>{title}</h1>
            </div>
          </div>
          <p className="mt-3 max-w-[40ch] text-[clamp(1rem,1.3vw,1.2rem)] font-medium leading-snug text-fg-2 max-sm:text-[0.95rem]">{line}</p>

          {r && (
            <>
              <div className="mt-4">
                <ScoreRing correct={r.correctAnswers} total={r.totalQuestions} passed={passed} />
              </div>

              <dl className="mt-4 grid w-full max-w-[30rem] grid-cols-2 gap-2.5 max-sm:gap-2">
                <div className="rounded-2xl bg-white/[0.05] px-2 py-3 ring-1 ring-white/10">
                  <dt className="text-[clamp(0.68rem,0.8vw,0.78rem)] font-bold uppercase tracking-[0.1em] text-fg-3">Toplanan xal</dt>
                  <dd className="mt-1 font-display text-[clamp(1.2rem,1.8vw,1.6rem)] font-extrabold tabular-nums text-sun max-sm:text-[1.1rem]" data-testid="points">{r.pointsEarned} / {r.maxPoints}</dd>
                </div>
                <div className="rounded-2xl bg-white/[0.05] px-2 py-3 ring-1 ring-white/10">
                  <dt className="text-[clamp(0.68rem,0.8vw,0.78rem)] font-bold uppercase tracking-[0.1em] text-fg-3">Keçid balı</dt>
                  <dd className="mt-1 font-display text-[clamp(1.2rem,1.8vw,1.6rem)] font-extrabold tabular-nums max-sm:text-[1.1rem]" data-testid="passing-score">{r.passingScore} / {r.totalQuestions}</dd>
                </div>
              </dl>

              <p className="mt-3 text-[clamp(0.95rem,1.15vw,1.1rem)] font-semibold text-fg-2 max-sm:text-[0.92rem]" data-testid="leaderboard-position">
                {r.leaderboardPosition === null
                  ? 'Lider cədvəlində mövqe hazırda hesablanmadı'
                  : `Lider cədvəlində yeriniz: ${formatRank(r.leaderboardPosition)} yer`}
              </p>

              {passed && r.rewardTitle && (
                <div data-testid="reward" className="pop mt-4 flex w-full max-w-[36rem] items-center gap-4 rounded-2xl bg-[linear-gradient(135deg,rgba(255,201,61,0.18),rgba(123,97,255,0.14))] px-4 py-3 text-left ring-1 ring-sun/40 [animation-delay:320ms]">
                  <RewardMedal className="size-12 shrink-0 max-sm:size-10" />
                  <div className="min-w-0">
                    <p className="text-[clamp(0.72rem,0.85vw,0.8rem)] font-bold uppercase tracking-[0.14em] text-sun">Mükafat</p>
                    <p lang="az" className="font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-bold leading-tight [overflow-wrap:anywhere]">{r.rewardTitle}</p>
                  </div>
                </div>
              )}
            </>
          )}

          {error && <p role="alert" className="mt-4 rounded-2xl bg-bad/15 px-4 py-2.5 text-[clamp(0.92rem,1.05vw,1rem)] font-semibold ring-1 ring-bad/60">{error}</p>}
        </section>

        <SidePanel load={load} retry={retry} review={r?.review ?? []} />
      </div>

      <nav aria-label="Nəticə seçimləri" data-testid="result-actions" className="rise flex w-full items-stretch gap-4 [animation-delay:160ms] max-sm:flex-col max-sm:gap-3">
        <button type="button" onClick={() => campaignId && go(`/register/${campaignId}`, true)} disabled={navigating || campaignId === null} className={`${PRIMARY_CTA} flex-[1.5]`} data-testid="result-next">Növbəti iştirakçı</button>
        <button type="button" onClick={() => campaignId && go(`/leaderboard/${campaignId}`, false)} disabled={navigating || campaignId === null} className={`${SECONDARY_CTA} flex-1`} data-testid="result-leaderboard">
          <TrophyIcon className="size-[1.1em] shrink-0" />
          Lider cədvəli
        </button>
        <button type="button" onClick={() => go('/', true)} disabled={navigating} className={`${SECONDARY_CTA} flex-1`} data-testid="result-home">Ana səhifə</button>
      </nav>
    </GameShowShell>
  )
}
