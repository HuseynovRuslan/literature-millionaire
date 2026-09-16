import { startTransition, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { HourglassMark } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import { useGame } from '../game/GameContext'

/** Shown inside /game when the backend no longer knows the stored session. */
export default function SessionExpired() {
  const navigate = useNavigate()
  const { state, reset, error } = useGame()
  const starting = state.status === 'starting'
  const navigationLocked = useRef(false)
  const [navigating, setNavigating] = useState(false)
  // The UI branches on the stable reason code, never on the (Azerbaijani, wording-subject-to-change) message text.
  // A plain "session not found" is already explained by the title below, so only the other reasons add detail.
  const reason = state.expiredReason && state.expiredReason !== 'SESSION_NOT_FOUND' ? state.expiredMessage : null
  // Kept only when the expired session safely carried its category; never invented.
  const campaignId = state.campaignId
  const quizModeTitle = state.quizMode?.title ?? null

  function go(path: string) {
    if (navigationLocked.current) return
    navigationLocked.current = true
    setNavigating(true)
    startTransition(() => {
      reset()
      navigate(path)
    })
  }

  return (
    <GameShowShell>
      <section
        data-testid="expired-stage"
        aria-labelledby="expired-title"
        className="home-stage rise flex min-h-[26.5625rem] flex-col items-center justify-center rounded-[clamp(1.2rem,1.6vw,1.44rem)] px-[clamp(1.5rem,4vw,3.6rem)] py-[2.125rem] text-center max-sm:min-h-[22rem] max-sm:rounded-2xl max-sm:px-5 max-sm:py-7"
      >
        <HourglassMark className="size-[clamp(5rem,8vw,7.2rem)] max-sm:size-20" />
        {quizModeTitle && (
          <p className="mt-3 font-display text-[clamp(0.85rem,1vw,0.9rem)] font-semibold uppercase tracking-[0.14em] text-[var(--p-gold-light)] max-sm:text-[0.75rem]" data-testid="expired-quiz-mode">
            {quizModeTitle}
          </p>
        )}
        <h1 id="expired-title" className="mt-[0.5312rem] font-display text-[clamp(2.6rem,4.6vw,4.14rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[2.1rem]">
          Sessiyanın vaxtı bitdi
        </h1>
        {reason && <p className="mt-2 text-[clamp(1.05rem,1.35vw,1.215rem)] font-semibold text-[var(--p-gold-light)] max-sm:text-[0.98rem]">{reason}</p>}
        <p className="mt-[0.85rem] max-w-[40rem] text-[clamp(1.15rem,1.6vw,1.44rem)] leading-relaxed text-[#d8dbe3] max-sm:text-base">
          Bu cəhd davam etdirilə bilmir. Növbəti iştirakçı qeydiyyatdan keçə bilər.
        </p>
        {error && (
          <p role="alert" className="mt-4 rounded-xl bg-[rgba(107,34,48,0.6)] px-5 py-3 text-[clamp(1rem,1.2vw,1.08rem)] text-[#fbf6ec] ring-1 ring-[#e8959c]">
            {error}
          </p>
        )}
      </section>

      <nav aria-label="Sessiya seçimləri" className="rise flex w-full max-w-[78rem] items-stretch justify-center gap-[clamp(0.8rem,1.4vw,1.26rem)] self-center [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
        <button
          type="button"
          onClick={() => go(campaignId ? `/register/${campaignId}` : '/')}
          disabled={starting || navigating}
          aria-busy={starting}
          className={`${PRIMARY_CTA} flex-[1.5] text-[clamp(1.6rem,2.5vw,2.25rem)]! disabled:opacity-60 max-sm:text-[1.35rem]!`}
          data-testid="expired-next"
        >
          {starting ? 'Oyun hazırlanır' : 'NÖVBƏTİ İŞTİRAKÇI'}
        </button>
        <button type="button" onClick={() => go('/')} disabled={starting || navigating} className={`${SECONDARY_CTA} flex-1 disabled:opacity-60`} data-testid="expired-home">
          ANA SƏHİFƏ
        </button>
      </nav>
    </GameShowShell>
  )
}
