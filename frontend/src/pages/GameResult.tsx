import { useNavigate } from 'react-router-dom'
import BrandMark from '../components/national/BrandMark'
import CarpetFrame from '../components/national/CarpetFrame'
import { Buta, ButaRule, Octagram } from '../components/national/Ornaments'
import { useGame } from '../game/GameContext'

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

/** Final screen shown inside /game once the quiz has ended. Visual only: all values come from state.result. */
export default function GameResult() {
  const navigate = useNavigate()
  const { state, startGame, reset, error } = useGame()
  const starting = state.status === 'starting'
  const r = state.result

  async function playAgain() {
    if (starting) return
    await startGame() // on success state.status becomes 'playing' and GamePage re-renders the question
  }

  function goHome() {
    reset()
    navigate('/')
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
        className="relative z-0 flex min-h-0 flex-1 flex-col items-center justify-center"
        style={{ padding: 'calc(var(--frame) + 0.8rem) calc(var(--frame) + 2rem) calc(var(--frame) + 4.2rem)' }}
      >
        <section className="rise relative w-full max-w-[64rem]">
          <Buta className="absolute -left-3 -top-4 z-10 h-12 w-9" flip />
          <Buta className="absolute -right-3 -top-4 z-10 h-12 w-9" />
          <Buta className="absolute -bottom-4 -left-3 z-10 h-12 w-9 rotate-180" />
          <Buta className="absolute -bottom-4 -right-3 z-10 h-12 w-9 rotate-180" flip />

          <div className="flex flex-col items-center rounded-xl border-[3px] border-[var(--p-gold)] bg-white px-[clamp(1.5rem,4vw,4rem)] py-[clamp(1.2rem,2.6vh,2.4rem)] text-center shadow-[var(--p-shadow)] outline outline-1 outline-offset-[-9px] outline-[var(--p-gold-light)]">
            {passed ? (
              <Octagram className="h-[clamp(4rem,7vh,5.5rem)] w-[clamp(4rem,7vh,5.5rem)]" />
            ) : (
              <OpenBook className="h-[clamp(3.6rem,6.5vh,5rem)] w-auto" />
            )}
            <h1 className={`mt-2 font-display text-[clamp(2.6rem,6vw,5.6rem)] font-bold leading-none ${passed ? 'text-[var(--p-burgundy)]' : 'text-[var(--p-indigo)]'}`}>
              {title}
            </h1>
            <p className="mt-2 max-w-[40ch] text-[clamp(1.05rem,1.5vw,1.55rem)] leading-relaxed text-[var(--p-ink-2)]">{line}</p>

            <ButaRule className="my-[clamp(0.6rem,1.6vh,1.4rem)] w-full max-w-[30rem]" />

            {r && (
              <>
                <p className="text-[clamp(0.95rem,1.3vw,1.3rem)] font-medium text-[var(--p-ink-2)]">Düzgün cavab</p>
                <p
                  className={`mt-1 font-display text-[clamp(3.6rem,8vw,8rem)] font-bold leading-none tabular-nums ${passed ? 'text-[var(--p-burgundy)]' : 'text-[var(--p-indigo)]'}`}
                  data-testid="score"
                >
                  {r.correctAnswers}
                  <span className="text-[0.5em] text-[var(--p-ink-2)]"> / {r.totalQuestions}</span>
                </p>

                <p className="mt-3 rounded-full border border-[var(--p-line)] bg-[var(--p-paper)] px-6 py-2 text-[clamp(1.05rem,1.6vw,1.65rem)] text-[var(--p-ink)]" data-testid="points">
                  Toplanan xal: <span className="font-semibold tabular-nums text-[var(--p-burgundy)]">{r.pointsEarned}</span> / {r.maxPoints}
                </p>
                <p className="mt-2 text-[clamp(0.95rem,1.3vw,1.3rem)] text-[var(--p-ink-2)]">
                  Keçid üçün ən azı {r.passingScore} / {r.totalQuestions} düzgün cavab lazımdır.
                </p>

                {passed && r.rewardTitle && (
                  <p className="mt-4 rounded-2xl border-2 border-[var(--p-gold)] bg-[var(--p-paper)] px-8 py-3 font-display text-[clamp(1.3rem,2vw,2rem)] font-semibold text-[var(--p-burgundy)]" data-testid="reward">
                    Mükafat: {r.rewardTitle}
                  </p>
                )}
              </>
            )}

            <div className="mt-[clamp(1rem,2.6vh,2.2rem)] flex w-full max-w-[44rem] flex-col gap-4 sm:flex-row">
              <button
                type="button"
                onClick={playAgain}
                disabled={starting}
                aria-busy={starting}
                className="tap paper-cta flex min-h-[6rem] flex-1 items-center justify-center rounded-full font-display text-[clamp(1.6rem,2.6vw,2.6rem)] font-bold tracking-[0.04em]"
              >
                {starting ? 'Oyun hazırlanır' : 'Yenidən oyna'}
              </button>
              <button
                type="button"
                onClick={goHome}
                disabled={starting}
                className="tap paper-ghost flex min-h-[6rem] flex-1 items-center justify-center rounded-full font-display text-[clamp(1.6rem,2.6vw,2.6rem)] font-semibold disabled:opacity-60"
              >
                Ana səhifə
              </button>
            </div>

            {error && (
              <p role="alert" className="mt-4 rounded-2xl border-2 border-[#b32a31] bg-white px-5 py-3 text-[clamp(1rem,1.3vw,1.3rem)] text-[var(--p-ink)]">
                {error}
              </p>
            )}
          </div>
        </section>
      </div>

      {/* Brand mark only on wide screens: at 1024x768 the card fills the height and the mark would touch its frame. */}
      <div className="absolute z-20 hidden xl:block" style={{ right: 'calc(var(--frame) + 1rem)', bottom: 'calc(var(--frame) + 0.7rem)' }}>
        <BrandMark />
      </div>
    </main>
  )
}
