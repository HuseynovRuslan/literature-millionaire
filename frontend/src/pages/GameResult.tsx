import { startTransition, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { OpenBookMark, RewardMedal, VictoryStar } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import RankMedal from '../components/home/RankMedal'
import { useGame } from '../game/GameContext'
import { useLeaderboard, type LeaderboardLoad } from '../hooks/useLeaderboard'
import { formatRank } from '../utils/leaderboard'

const STAGE = 'home-stage rise rounded-[clamp(1.2rem,1.6vw,2rem)] max-sm:rounded-2xl'
/** Three result buttons share one row on the kiosk, so the long labels use a slightly smaller type. */
const NAV_TEXT = 'text-[clamp(1.35rem,2vw,2.5rem)]! max-sm:text-[1.35rem]!'

function TopFive({ load, retry }: { load: LeaderboardLoad; retry: () => void }) {
  return (
    <section
      data-testid="top-five"
      aria-labelledby="top-five-title"
      className={`${STAGE} flex min-h-0 flex-col px-[clamp(1.2rem,2.2vw,2.6rem)] py-[clamp(1rem,2.4vh,2.2rem)] [animation-delay:60ms] max-sm:px-4 max-sm:py-4`}
    >
      <p className="text-[clamp(0.8rem,0.95vw,1.05rem)] font-semibold uppercase tracking-[0.14em] text-[var(--p-gold-light)] max-sm:text-[0.72rem]">KAMPANİYA NƏTİCƏLƏRİ</p>
      <h2 id="top-five-title" className="font-display text-[clamp(1.9rem,min(2.6vw,4.6vh),3.2rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[1.6rem]">İlk beşlik</h2>

      <div className="mt-[clamp(0.6rem,1.6vh,1.2rem)] flex min-h-[clamp(12rem,30vh,20rem)] flex-1 flex-col justify-center max-sm:min-h-[10rem]">
        {load.kind === 'idle' && (
          <p className="text-center font-display text-[clamp(1.3rem,1.8vw,2rem)] font-semibold text-[#d6deec]">Lider cədvəli hazırda əlçatan deyil</p>
        )}

        {load.kind === 'loading' && (
          <div role="status" aria-live="polite" className="flex flex-col gap-[clamp(0.35rem,0.8vh,0.6rem)]">
            <p className="text-center font-medium text-[#d6deec]">Lider cədvəli yüklənir…</p>
            {[1, 2, 3, 4, 5].map((row) => <span key={row} aria-hidden className="h-[clamp(2.4rem,5.2vh,3.6rem)] animate-pulse rounded-xl bg-white/[0.07] motion-reduce:animate-none" />)}
          </div>
        )}

        {load.kind === 'error' && (
          <div role="alert" className="flex flex-col items-center text-center">
            <p className="font-display text-[clamp(1.3rem,1.8vw,2rem)] font-semibold text-[#fbf6ec]">Lider cədvəlini yükləmək mümkün olmadı</p>
            <button type="button" onClick={retry} className={`${SECONDARY_CTA} mt-4 min-h-[clamp(4.5rem,8vh,5.5rem)]! px-10`}>Yenidən yoxla</button>
          </div>
        )}

        {load.kind === 'ready' && load.data.entries.length === 0 && (
          <p className="text-center font-display text-[clamp(1.3rem,1.8vw,2rem)] font-semibold text-[#d6deec]">Hələ tamamlanmış nəticə yoxdur</p>
        )}

        {load.kind === 'ready' && load.data.entries.length > 0 && (
          <ol className="flex flex-col gap-[clamp(0.35rem,0.8vh,0.6rem)]" aria-label="İlk beş iştirakçı">
            {load.data.entries.map((entry) => (
              <li key={entry.rank} className="grid min-h-[clamp(2.8rem,5.6vh,4rem)] grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-[clamp(0.7rem,1vw,1.1rem)] rounded-xl bg-white/[0.07] px-[clamp(0.7rem,1vw,1.1rem)] py-1 ring-1 ring-white/10 max-sm:gap-2.5 max-sm:px-2.5">
                <RankMedal rank={entry.rank} compact />
                <span className="min-w-0 font-semibold text-[clamp(1.05rem,1.35vw,1.5rem)] text-[#fbf6ec] [overflow-wrap:anywhere] max-sm:text-[0.98rem]">{entry.displayName}</span>
                <span className="text-right tabular-nums leading-tight">
                  <strong className="block font-display text-[clamp(1.2rem,1.6vw,1.8rem)] text-[var(--p-gold-light)] max-sm:text-[1.05rem]">{entry.pointsEarned}/{entry.maxPoints} xal</strong>
                  <span className="text-[clamp(0.8rem,0.95vw,1.05rem)] text-[#c9d3e6] max-sm:text-[0.78rem]">{entry.correctAnswers}/{entry.totalQuestions} düzgün</span>
                </span>
              </li>
            ))}
          </ol>
        )}
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

  return (
    <GameShowShell>
      <div data-result={passed ? 'passed' : 'failed'} className="grid min-h-0 grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)] gap-[clamp(1rem,1.8vw,2rem)] max-lg:grid-cols-1 max-sm:gap-4">
        <section
          data-testid="result-stage"
          aria-labelledby="result-title"
          className={`${STAGE} flex min-h-0 flex-col items-center justify-center px-[clamp(1.4rem,3vw,4rem)] py-[clamp(1rem,2.6vh,2.6rem)] text-center max-sm:px-4 max-sm:py-5`}
        >
          {passed
            ? <VictoryStar className="size-[clamp(3.6rem,min(6vw,10vh),7rem)] max-sm:size-16" />
            : <OpenBookMark className="h-[clamp(3rem,min(5vw,8vh),5.5rem)] w-auto max-sm:h-12" />}
          <h1 id="result-title" className={`mt-[clamp(0.3rem,0.8vh,0.7rem)] font-display text-[clamp(2.4rem,min(4.2vw,7.4vh),5.2rem)] font-bold leading-none max-sm:text-[2.1rem] ${passed ? 'text-[var(--p-gold-light)]' : 'text-[#fbf6ec]'}`}>{title}</h1>
          <p className="mt-[clamp(0.3rem,0.8vh,0.6rem)] max-w-[40ch] text-[clamp(1.05rem,min(1.4vw,2.4vh),1.6rem)] leading-snug text-[#d6deec] max-sm:text-[0.98rem]">{line}</p>

          {r && (
            <>
              <p className="mt-[clamp(0.6rem,1.6vh,1.3rem)] text-[clamp(0.85rem,1vw,1.1rem)] font-semibold uppercase tracking-[0.14em] text-[var(--p-gold-light)] max-sm:mt-3 max-sm:text-[0.75rem]">Düzgün cavab</p>
              <p className="font-display text-[clamp(3.6rem,min(6vw,10.5vh),7.5rem)] font-bold leading-none tabular-nums text-[#fbf6ec] max-sm:text-[3.4rem]" data-testid="score">
                {r.correctAnswers}<span className="text-[0.5em] text-[#c9d3e6]"> / {r.totalQuestions}</span>
              </p>

              <dl className="mt-[clamp(0.6rem,1.6vh,1.3rem)] grid w-full max-w-[40rem] grid-cols-2 divide-x divide-[rgba(233,192,105,0.35)] overflow-hidden rounded-2xl bg-white/[0.07] ring-1 ring-[rgba(233,192,105,0.4)] max-sm:mt-3 max-sm:rounded-xl">
                <div className="flex flex-col justify-center gap-1 px-[clamp(0.8rem,1.4vw,1.6rem)] py-[clamp(0.5rem,1.2vh,0.9rem)] max-sm:px-2 max-sm:py-2">
                  <dt className="text-[clamp(0.78rem,0.9vw,1rem)] font-semibold uppercase tracking-[0.1em] text-[var(--p-gold-light)] max-sm:text-[0.68rem]">Toplanan xal</dt>
                  <dd className="font-display text-[clamp(1.5rem,min(2.2vw,4vh),2.6rem)] font-bold leading-tight tabular-nums text-[#fbf6ec] max-sm:text-[1.3rem]" data-testid="points">{r.pointsEarned} / {r.maxPoints}</dd>
                </div>
                <div className="flex flex-col justify-center gap-1 px-[clamp(0.8rem,1.4vw,1.6rem)] py-[clamp(0.5rem,1.2vh,0.9rem)] max-sm:px-2 max-sm:py-2">
                  <dt className="text-[clamp(0.78rem,0.9vw,1rem)] font-semibold uppercase tracking-[0.1em] text-[var(--p-gold-light)] max-sm:text-[0.68rem]">Keçid balı</dt>
                  <dd className="font-display text-[clamp(1.5rem,min(2.2vw,4vh),2.6rem)] font-bold leading-tight tabular-nums text-[#fbf6ec] max-sm:text-[1.3rem]" data-testid="passing-score">{r.passingScore} / {r.totalQuestions}</dd>
                </div>
              </dl>

              <p className="mt-[clamp(0.5rem,1.3vh,1rem)] text-[clamp(1.05rem,min(1.35vw,2.4vh),1.5rem)] font-semibold text-[#fbf6ec] max-sm:text-[0.98rem]" data-testid="leaderboard-position">
                {r.leaderboardPosition === null
                  ? 'Lider cədvəlində mövqe hazırda hesablanmadı'
                  : `Lider cədvəlində yeriniz: ${formatRank(r.leaderboardPosition)} yer`}
              </p>

              {passed && r.rewardTitle && (
                <div data-testid="reward" className="mt-[clamp(0.6rem,1.6vh,1.3rem)] flex items-center gap-[clamp(0.7rem,1vw,1.1rem)] rounded-2xl bg-[rgba(243,215,126,0.12)] px-[clamp(1rem,1.6vw,1.8rem)] py-[clamp(0.5rem,1.1vh,0.9rem)] text-left ring-1 ring-[rgba(243,215,126,0.6)] max-sm:mt-3 max-sm:rounded-xl max-sm:px-3 max-sm:py-2">
                  <RewardMedal className="h-[clamp(2.8rem,min(4vw,6.6vh),4.6rem)] w-auto shrink-0 max-sm:h-11" />
                  <div className="min-w-0">
                    <p className="text-[clamp(0.78rem,0.9vw,1rem)] font-semibold uppercase tracking-[0.14em] text-[var(--p-gold-light)] max-sm:text-[0.68rem]">Mükafat</p>
                    <p lang="az" className="font-display text-[clamp(1.35rem,min(1.9vw,3.4vh),2.3rem)] font-bold leading-tight text-[#fbf6ec] [overflow-wrap:anywhere] max-sm:text-[1.15rem]">{r.rewardTitle}</p>
                  </div>
                </div>
              )}
            </>
          )}

          {error && <p role="alert" className="mt-3 rounded-xl bg-[rgba(125,22,29,0.6)] px-4 py-2 text-[clamp(0.95rem,1.1vw,1.2rem)] text-[#fbf6ec] ring-1 ring-[#ff9aa2]">{error}</p>}
        </section>

        <TopFive load={load} retry={retry} />
      </div>

      <nav aria-label="Nəticə seçimləri" data-testid="result-actions" className="rise flex w-full max-w-[96rem] items-stretch justify-center gap-[clamp(0.8rem,1.4vw,1.5rem)] self-center [animation-delay:120ms] max-sm:flex-col max-sm:gap-3">
        <button type="button" onClick={() => go('/register', true)} disabled={navigating} className={`${PRIMARY_CTA} ${NAV_TEXT} flex-[1.4] disabled:opacity-60`} data-testid="result-next">NÖVBƏTİ İŞTİRAKÇI</button>
        <button type="button" onClick={() => campaignId && go(`/leaderboard/${campaignId}`, false)} disabled={navigating || campaignId === null} className={`${SECONDARY_CTA} flex-1 disabled:opacity-60`} data-testid="result-leaderboard">LİDER CƏDVƏLİ</button>
        <button type="button" onClick={() => go('/', true)} disabled={navigating} className={`${SECONDARY_CTA} flex-1 disabled:opacity-60`} data-testid="result-home">ANA SƏHİFƏ</button>
      </nav>
    </GameShowShell>
  )
}
