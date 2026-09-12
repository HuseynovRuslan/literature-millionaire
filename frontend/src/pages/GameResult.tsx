import { useNavigate } from 'react-router-dom'
import GoldRule from '../components/GoldRule'
import { useGame } from '../game/GameContext'

/** Final screen shown inside /game once the quiz has ended. */
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
    <main className="kiosk ornament flex flex-col items-center justify-center px-6 text-center">
      <div className="rise flex w-full max-w-[60rem] flex-col items-center">
        <h1 className="font-display text-[clamp(3.5rem,8vw,8.5rem)] font-bold leading-none text-ivory">{title}</h1>
        <p className="mt-4 text-[clamp(1.1rem,1.6vw,1.7rem)] text-mist">{line}</p>

        <GoldRule className="my-8 w-full max-w-[30rem]" />

        {r && (
          <>
            <p className="text-[clamp(1rem,1.4vw,1.4rem)] text-gold-light">Düzgün cavab</p>
            <p className={`mt-1 font-display text-[clamp(4rem,9vw,9.5rem)] font-bold leading-none tabular-nums ${passed ? 'text-gold' : 'text-ivory'}`}>
              {r.correctAnswers}
              <span className="text-[0.5em] text-mist"> / {r.totalQuestions}</span>
            </p>
            <p className="mt-4 text-[clamp(1.2rem,1.9vw,2rem)] text-ivory" data-testid="points">
              Toplanan xal: <span className="font-semibold tabular-nums text-gold-light">{r.pointsEarned}</span> / {r.maxPoints}
            </p>
            <p className="mt-3 text-[clamp(1rem,1.4vw,1.4rem)] text-mist">
              Keçid üçün ən azı {r.passingScore} / {r.totalQuestions} düzgün cavab lazımdır.
            </p>
            {passed && r.rewardTitle && (
              <p className="mt-6 rounded-2xl border border-gold/60 bg-gold/10 px-8 py-4 font-display text-[clamp(1.4rem,2.2vw,2.2rem)] font-semibold text-gold-light">
                Mükafat: {r.rewardTitle}
              </p>
            )}
          </>
        )}

        <div className="mt-14 flex w-full max-w-[44rem] flex-col gap-5 sm:flex-row">
          <button
            type="button"
            onClick={playAgain}
            disabled={starting}
            aria-busy={starting}
            className="tap flex min-h-[6rem] flex-1 items-center justify-center rounded-2xl bg-gold font-display text-[clamp(1.6rem,2.6vw,2.6rem)] font-bold text-navy-900 shadow-[0_18px_50px_-12px_rgba(212,168,59,0.55)] disabled:bg-gold/70"
          >
            {starting ? 'Oyun hazırlanır' : 'Yenidən oyna'}
          </button>
          <button
            type="button"
            onClick={goHome}
            disabled={starting}
            className="tap flex min-h-[6rem] flex-1 items-center justify-center rounded-2xl border-2 border-gold/70 font-display text-[clamp(1.6rem,2.6vw,2.6rem)] font-semibold text-gold-light"
          >
            Ana səhifə
          </button>
        </div>

        {error && (
          <p role="alert" className="mt-6 rounded-xl border border-bad/50 bg-bad/10 px-5 py-3 text-[clamp(1rem,1.3vw,1.3rem)]">
            {error}
          </p>
        )}
      </div>
    </main>
  )
}
