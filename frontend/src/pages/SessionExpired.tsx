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
        className="card rise mx-auto flex min-h-[24rem] w-full max-w-[56rem] flex-col items-center justify-center rounded-[2rem] px-[clamp(1.5rem,4vw,3.5rem)] py-12 text-center max-sm:min-h-[20rem] max-sm:rounded-3xl max-sm:px-5 max-sm:py-9"
      >
        <div className="pop"><HourglassMark className="bob size-[clamp(6rem,9vw,8rem)] max-sm:size-24" /></div>
        {quizModeTitle && (
          <p className="chip mt-5 px-4 py-1.5 text-[clamp(0.75rem,0.9vw,0.88rem)] uppercase tracking-[0.14em] text-brand-soft" data-testid="expired-quiz-mode">
            {quizModeTitle}
          </p>
        )}
        <h1 id="expired-title" className="mt-4 font-display text-[clamp(2rem,3.8vw,3.2rem)] font-extrabold leading-tight max-sm:text-[1.7rem]">
          Sessiyanın vaxtı bitdi
        </h1>
        {reason && <p className="mt-2 text-[clamp(1rem,1.25vw,1.15rem)] font-bold text-sun max-sm:text-[0.95rem]">{reason}</p>}
        <p className="mt-3 max-w-[40rem] text-[clamp(1.05rem,1.4vw,1.3rem)] leading-relaxed text-fg-2 max-sm:text-base">
          Bu cəhd davam etdirilə bilmir. Növbəti iştirakçı qeydiyyatdan keçə bilər.
        </p>
        {error && (
          <p role="alert" className="mt-4 rounded-2xl bg-bad/15 px-5 py-3 text-[clamp(0.95rem,1.1vw,1.05rem)] font-semibold ring-1 ring-bad/60">
            {error}
          </p>
        )}
      </section>

      <nav aria-label="Sessiya seçimləri" className="rise mx-auto flex w-full max-w-[56rem] items-stretch gap-4 [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
        <button
          type="button"
          onClick={() => go(campaignId ? `/register/${campaignId}` : '/')}
          disabled={starting || navigating}
          aria-busy={starting}
          className={`${PRIMARY_CTA} flex-[1.5]`}
          data-testid="expired-next"
        >
          {starting ? 'Oyun hazırlanır' : 'Növbəti iştirakçı'}
        </button>
        <button type="button" onClick={() => go('/')} disabled={starting || navigating} className={`${SECONDARY_CTA} flex-1`} data-testid="expired-home">
          Ana səhifə
        </button>
      </nav>
    </GameShowShell>
  )
}
