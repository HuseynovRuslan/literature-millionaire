import BrandMark from '../national/BrandMark'
import { PRODUCT_NAME } from '../national/KioskBrand'
import GameTimer from './GameTimer'

/**
 * Compact game header: brand and category, "Sual N / 10" with a segmented progress bar, and the timer.
 * Progress comes from the server's questionNumber / totalQuestions; nothing about score or correctness.
 */
export default function GameStageHeader({
  questionNumber,
  totalQuestions,
  remainingSec,
  fraction,
  urgent,
  expired,
  quizModeTitle,
  soundOn,
  onToggleSound,
  onQuit,
}: {
  questionNumber: number
  totalQuestions: number
  remainingSec: number
  fraction: number
  urgent: boolean
  expired: boolean
  /** Category the session is playing, from the server's start/session data. Empty string renders nothing. */
  quizModeTitle: string
  /** Whether the atmosphere is audible; the toggle is here so a venue can silence the kiosk mid-round. */
  soundOn: boolean
  onToggleSound: () => void
  /**
   * Leave the quiz and sign out. Asked for by name: a person who reaches this screen as somebody else - a
   * shared phone, an admin's sign-in - had no way off it, and a quiz nobody is playing runs its clock down
   * ten seconds at a time. The button only asks; GamePage does the confirming.
   */
  onQuit: () => void
}) {
  return (
    <header
      data-testid="game-header"
      className="grid shrink-0 grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-x-[clamp(1rem,2.4vw,3rem)] [grid-template-areas:'brand_progress_timer'] max-sm:grid-cols-[minmax(0,1fr)_auto] max-sm:gap-x-3 max-sm:gap-y-2.5 max-sm:[grid-template-areas:'brand_timer'_'progress_timer']"
    >
      <div data-testid="kiosk-brand" className="flex min-w-0 items-center gap-[clamp(0.6rem,0.9vw,1rem)] [grid-area:brand]">
        <BrandMark size="sm" className="max-sm:min-w-0 max-sm:shrink" />
        <div className="min-w-0 max-w-[18rem]">
          <p lang="az" data-testid="product-name" className="whitespace-nowrap font-display text-[clamp(1rem,1.3vw,1.4rem)] font-extrabold uppercase leading-none tracking-tight max-sm:text-[0.9rem]">
            {PRODUCT_NAME}
          </p>
          {quizModeTitle && (
            <p lang="az" data-testid="game-quiz-mode" className="mt-1 truncate text-[clamp(0.72rem,0.85vw,0.9rem)] font-bold leading-none text-brand-soft max-sm:text-[0.66rem]">
              {quizModeTitle}
            </p>
          )}
        </div>

        <button
          type="button"
          onClick={onToggleSound}
          aria-pressed={soundOn}
          aria-label={soundOn ? 'Səsi söndür' : 'Səsi aç'}
          title={soundOn ? 'Səsi söndür' : 'Səsi aç'}
          data-testid="sound-toggle"
          data-sound={soundOn ? 'on' : 'off'}
          className="icon-btn ml-[clamp(0.2rem,0.6vw,0.8rem)] size-[clamp(2.75rem,3.2vw,3.1rem)] shrink-0 max-sm:size-10"
        >
          <svg viewBox="0 0 24 24" className="size-[52%]" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M4 9.5h3.2L12 5.5v13l-4.8-4H4a.5.5 0 0 1-.5-.5v-4a.5.5 0 0 1 .5-.5Z" />
            {soundOn ? <path d="M15.6 9a4.2 4.2 0 0 1 0 6M18.2 6.6a7.6 7.6 0 0 1 0 10.8" /> : <path d="m16 9.5 4.5 5M20.5 9.5 16 14.5" />}
          </svg>
        </button>

        <button
          type="button"
          onClick={onQuit}
          aria-label="Yarışdan çıx"
          title="Yarışdan çıx"
          data-testid="game-quit"
          className="icon-btn ml-[clamp(0.15rem,0.4vw,0.5rem)] size-[clamp(2.75rem,3.2vw,3.1rem)] shrink-0 max-sm:size-10"
        >
          <svg viewBox="0 0 24 24" className="size-[52%]" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M14 4.5H6.5a1 1 0 0 0-1 1v13a1 1 0 0 0 1 1H14" />
            <path d="M17.5 8.5 21 12l-3.5 3.5M20.5 12H10" />
          </svg>
        </button>
      </div>

      <div className="min-w-0 [grid-area:progress]">
        <div className="flex items-baseline justify-between gap-3">
          <p data-testid="question-counter" className="font-display text-[clamp(1.3rem,2vw,2.3rem)] font-extrabold leading-none max-sm:text-[1.05rem]">
            Sual {questionNumber} <span className="text-fg-3">/ {totalQuestions}</span>
          </p>
        </div>
        <div
          role="progressbar"
          aria-label={`Sual ${questionNumber} / ${totalQuestions}`}
          aria-valuemin={1}
          aria-valuemax={totalQuestions}
          aria-valuenow={questionNumber}
          data-testid="game-progress"
          className="mt-[clamp(0.5rem,1vh,0.8rem)] flex gap-[clamp(0.25rem,0.4vw,0.45rem)] max-sm:mt-1.5 max-sm:gap-1"
        >
          {Array.from({ length: totalQuestions }, (_, i) => (
            <span
              key={i}
              aria-hidden="true"
              className={`h-[clamp(0.55rem,1vh,0.8rem)] flex-1 rounded-full transition-colors duration-300 max-sm:h-1.5 ${
                i < questionNumber - 1
                  ? 'bg-brand'
                  : i === questionNumber - 1
                    ? 'pill-fill bg-sun shadow-[0_0_0.8rem_rgba(255,201,61,0.7)]'
                    : 'bg-white/12'
              }`}
            />
          ))}
        </div>
      </div>

      <GameTimer className="[grid-area:timer]" seconds={remainingSec} fraction={fraction} urgent={urgent} expired={expired} />
    </header>
  )
}
