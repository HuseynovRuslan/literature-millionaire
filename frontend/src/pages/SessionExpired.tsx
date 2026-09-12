import GoldRule from '../components/GoldRule'
import { useGame } from '../game/GameContext'

/** Shown inside /game when the backend no longer knows the stored session. */
export default function SessionExpired() {
  const { state, startGame, error } = useGame()
  const starting = state.status === 'starting'

  return (
    <main className="kiosk ornament flex flex-col items-center justify-center px-6 text-center">
      <div className="rise flex w-full max-w-[56rem] flex-col items-center">
        <h1 className="font-display text-[clamp(2.4rem,5vw,5.5rem)] font-bold leading-tight text-ivory">
          {state.expiredMessage ?? 'Oyun sessiyasının vaxtı bitib.'}
        </h1>
        <GoldRule className="my-8 w-full max-w-[30rem]" />
        <p className="max-w-[36rem] text-[clamp(1.1rem,1.6vw,1.7rem)] leading-relaxed text-mist">
          Əvvəlki oyun davam etdirilə bilməz. Yeni oyun başladın.
        </p>
        <button
          type="button"
          onClick={() => { if (!starting) void startGame() }}
          disabled={starting}
          aria-busy={starting}
          className="tap mt-12 flex min-h-[7.5rem] w-full max-w-[34rem] items-center justify-center rounded-2xl bg-gold px-10 font-display text-[clamp(2rem,3.4vw,3.4rem)] font-bold tracking-[0.06em] text-navy-900 shadow-[0_18px_50px_-12px_rgba(212,168,59,0.55)] disabled:bg-gold/70"
        >
          {starting ? 'Oyun hazırlanır' : 'YENİ OYUN'}
        </button>
        {error && (
          <p role="alert" className="mt-6 rounded-xl border border-bad/50 bg-bad/10 px-5 py-3 text-[clamp(1rem,1.3vw,1.3rem)]">
            {error}
          </p>
        )}
      </div>
    </main>
  )
}
