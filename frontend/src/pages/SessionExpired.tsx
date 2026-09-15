import { startTransition, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { HourglassMark } from '../components/home/GameShowArt'
import { PRIMARY_CTA, SECONDARY_CTA } from '../components/home/gameShowClasses'
import GameShowShell from '../components/home/GameShowShell'
import { useGame } from '../game/GameContext'

/** GameContext's message for a plain 404; the new title already says this, so only other reasons are shown. */
const GENERIC_EXPIRED_MESSAGE = 'Oyun sessiyasının vaxtı bitib.'

/** Shown inside /game when the backend no longer knows the stored session. */
export default function SessionExpired() {
  const navigate = useNavigate()
  const { state, reset, error } = useGame()
  const starting = state.status === 'starting'
  const navigationLocked = useRef(false)
  const [navigating, setNavigating] = useState(false)
  const reason = state.expiredMessage && state.expiredMessage !== GENERIC_EXPIRED_MESSAGE ? state.expiredMessage : null

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
        className="home-stage rise flex min-h-[clamp(20rem,50vh,34rem)] flex-col items-center justify-center rounded-[clamp(1.2rem,1.6vw,2rem)] px-[clamp(1.5rem,4vw,5rem)] py-[clamp(1.5rem,4vh,3.5rem)] text-center max-sm:min-h-[22rem] max-sm:rounded-2xl max-sm:px-5 max-sm:py-7"
      >
        <HourglassMark className="size-[clamp(5rem,min(8vw,12vh),8.5rem)] max-sm:size-20" />
        <h1 id="expired-title" className="mt-[clamp(0.8rem,2vh,1.4rem)] font-display text-[clamp(2.6rem,min(4.6vw,8vh),5.4rem)] font-bold leading-tight text-[#fbf6ec] max-sm:text-[2.1rem]">
          Sessiyanın vaxtı bitdi
        </h1>
        {reason && <p className="mt-2 text-[clamp(1.05rem,1.35vw,1.5rem)] font-semibold text-[var(--p-gold-light)] max-sm:text-[0.98rem]">{reason}</p>}
        <p className="mt-[clamp(0.6rem,1.6vh,1.1rem)] max-w-[40rem] text-[clamp(1.15rem,1.6vw,1.8rem)] leading-relaxed text-[#d6deec] max-sm:text-base">
          Bu cəhd davam etdirilə bilmir. Növbəti iştirakçı qeydiyyatdan keçə bilər.
        </p>
        {error && (
          <p role="alert" className="mt-4 rounded-xl bg-[rgba(125,22,29,0.6)] px-5 py-3 text-[clamp(1rem,1.2vw,1.3rem)] text-[#fbf6ec] ring-1 ring-[#ff9aa2]">
            {error}
          </p>
        )}
      </section>

      <nav aria-label="Sessiya seçimləri" className="rise flex w-full max-w-[78rem] items-stretch justify-center gap-[clamp(0.8rem,1.4vw,1.5rem)] self-center [animation-delay:90ms] max-sm:flex-col max-sm:gap-3">
        <button
          type="button"
          onClick={() => go('/register')}
          disabled={starting || navigating}
          aria-busy={starting}
          className={`${PRIMARY_CTA} flex-[1.5] text-[clamp(1.6rem,2.5vw,3rem)]! disabled:opacity-60 max-sm:text-[1.35rem]!`}
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
