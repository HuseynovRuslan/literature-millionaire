import GoldRule from '../components/GoldRule'
import { startTransition } from 'react'
import { useNavigate } from 'react-router-dom'
import KioskBrand from '../components/national/KioskBrand'
import { useGame } from '../game/GameContext'

/** Shown inside /game when the backend no longer knows the stored session. */
export default function SessionExpired() {
  const navigate = useNavigate()
  const { state, reset, error } = useGame()
  const starting = state.status === 'starting'

  return (
    <main className="kiosk ornament flex flex-col px-6 text-center">
      <header className="flex shrink-0 justify-start pt-[clamp(1rem,2.5vh,2rem)]">
        <KioskBrand tone="dark" />
      </header>
      <div className="flex min-h-0 flex-1 flex-col items-center justify-center">
        <div className="rise flex w-full max-w-[56rem] flex-col items-center">
          <h1 className="font-display text-[clamp(2.4rem,5vw,5.5rem)] font-bold leading-tight text-ivory">
            {state.expiredMessage ?? 'Oyun sessiyasının vaxtı bitib.'}
          </h1>
          <GoldRule className="my-8 w-full max-w-[30rem]" />
          <p className="max-w-[36rem] text-[clamp(1.1rem,1.6vw,1.7rem)] leading-relaxed text-mist">
            Bu cəhd davam etdirilə bilmir. Növbəti iştirakçı qeydiyyatdan keçə bilər.
          </p>
          <button
            type="button"
            onClick={() => startTransition(() => { reset(); navigate('/register') })}
            disabled={starting}
            aria-busy={starting}
            className="tap mt-12 flex min-h-[7.5rem] w-full max-w-[46rem] items-center justify-center whitespace-nowrap rounded-2xl bg-gold px-10 font-display text-[clamp(2rem,3.4vw,3.4rem)] font-bold tracking-[0.06em] text-navy-900 shadow-[0_18px_50px_-12px_rgba(212,168,59,0.55)] disabled:bg-gold/70"
          >
            {starting ? 'Oyun hazırlanır' : 'NÖVBƏTİ İŞTİRAKÇI'}
          </button>
          {error && (
            <p role="alert" className="mt-6 rounded-xl border border-bad/50 bg-bad/10 px-5 py-3 text-[clamp(1rem,1.3vw,1.3rem)]">
              {error}
            </p>
          )}
        </div>
      </div>
    </main>
  )
}
