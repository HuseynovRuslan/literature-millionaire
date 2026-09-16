import BrandMark from '../national/BrandMark'
import { PRODUCT_NAME } from '../national/KioskBrand'
import GameTimer from './GameTimer'

/**
 * Compact game header: brand, "Sual N / 10" with a segmented progress bar, and the timer medallion.
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
}) {
  return (
    <header
      data-testid="game-header"
      className="grid shrink-0 grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-x-[clamp(1.2rem,2.6vw,3.2rem)] [grid-template-areas:'brand_progress_timer'] max-sm:grid-cols-[minmax(0,1fr)_auto] max-sm:gap-x-3 max-sm:gap-y-2 max-sm:[grid-template-areas:'brand_timer'_'progress_timer']"
    >
      <div data-testid="kiosk-brand" className="flex min-w-0 items-center gap-[clamp(0.6rem,0.9vw,1rem)] [grid-area:brand]">
        <BrandMark size="sm" className="max-sm:min-w-0 max-sm:shrink" />
        {/* Same single line as before the category label was added (Task 15B): the category name sits
            inline after the product name and truncates on its own, so the header never grows taller. */}
        <div className="flex min-w-0 items-baseline gap-x-[clamp(0.4rem,0.6vw,0.6rem)]">
          <p
            lang="az"
            data-testid="product-name"
            className="shrink-0 whitespace-nowrap font-display text-[clamp(1.3rem,1.7vw,2rem)] font-bold uppercase leading-none tracking-[0.07em] text-[#fbf6ec] max-sm:text-[1rem] max-sm:tracking-[0.05em]"
          >
            {PRODUCT_NAME}
          </p>
          {quizModeTitle && (
            <p
              lang="az"
              data-testid="game-quiz-mode"
              className="min-w-0 truncate text-[clamp(0.62rem,0.78vw,0.85rem)] font-semibold uppercase leading-none tracking-[0.06em] text-[var(--p-gold-light)] max-sm:text-[0.58rem]"
            >
              · {quizModeTitle}
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
          className="tap ml-[clamp(0.4rem,0.8vw,0.9rem)] grid size-[clamp(2.1rem,2.8vw,2.8rem)] shrink-0 place-items-center rounded-full text-[#fbf6ec] ring-1 ring-[rgba(217,187,124,0.45)] max-sm:size-9"
        >
          <svg viewBox="0 0 24 24" className="size-[58%]" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M4 9.5h3.2L12 5.5v13l-4.8-4H4a.5.5 0 0 1-.5-.5v-4a.5.5 0 0 1 .5-.5Z" />
            {soundOn ? <path d="M15.6 9a4.2 4.2 0 0 1 0 6M18.2 6.6a7.6 7.6 0 0 1 0 10.8" /> : <path d="m16 9.5 4.5 5M20.5 9.5 16 14.5" />}
          </svg>
        </button>
      </div>

      <div className="min-w-0 [grid-area:progress]">
        <p data-testid="question-counter" className="font-display text-[clamp(1.6rem,2.3vw,2.8rem)] font-bold leading-none text-[#fbf6ec] max-sm:text-[1.2rem]">
          Sual {questionNumber} / {totalQuestions}
        </p>
        <div
          role="progressbar"
          aria-label={`Sual ${questionNumber} / ${totalQuestions}`}
          aria-valuemin={1}
          aria-valuemax={totalQuestions}
          aria-valuenow={questionNumber}
          data-testid="game-progress"
          className="mt-[clamp(0.45rem,0.9vh,0.75rem)] flex gap-[clamp(0.25rem,0.4vw,0.45rem)] max-sm:mt-1.5 max-sm:gap-1"
        >
          {Array.from({ length: totalQuestions }, (_, i) => (
            <span
              key={i}
              aria-hidden="true"
              className={`h-[clamp(0.45rem,0.9vh,0.7rem)] flex-1 rounded-full max-sm:h-1.5 ${
                i < questionNumber - 1 ? 'bg-[#b8955a]' : i === questionNumber - 1 ? 'bg-[#e8d29c] shadow-[0_0_0.6rem_rgba(232,210,156,0.7)]' : 'bg-white/15'
              }`}
            />
          ))}
        </div>
      </div>

      <GameTimer className="[grid-area:timer]" seconds={remainingSec} fraction={fraction} urgent={urgent} expired={expired} />
    </header>
  )
}
