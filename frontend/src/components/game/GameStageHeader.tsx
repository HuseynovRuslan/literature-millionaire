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
}: {
  questionNumber: number
  totalQuestions: number
  remainingSec: number
  fraction: number
  urgent: boolean
  expired: boolean
}) {
  return (
    <header
      data-testid="game-header"
      className="grid shrink-0 grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-x-[clamp(1.2rem,2.6vw,3.2rem)] [grid-template-areas:'brand_progress_timer'] max-sm:grid-cols-[minmax(0,1fr)_auto] max-sm:gap-x-3 max-sm:gap-y-2 max-sm:[grid-template-areas:'brand_timer'_'progress_timer']"
    >
      <div data-testid="kiosk-brand" className="flex min-w-0 items-center gap-[clamp(0.6rem,0.9vw,1rem)] [grid-area:brand]">
        <BrandMark size="sm" className="max-sm:min-w-0 max-sm:shrink" />
        <p
          lang="az"
          data-testid="product-name"
          className="whitespace-nowrap font-display text-[clamp(1.3rem,1.7vw,2rem)] font-bold uppercase leading-none tracking-[0.07em] text-[#fbf6ec] max-sm:text-[1rem] max-sm:tracking-[0.05em]"
        >
          {PRODUCT_NAME}
        </p>
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
                i < questionNumber - 1 ? 'bg-[#cf9c3c]' : i === questionNumber - 1 ? 'bg-[#f3d77e] shadow-[0_0_0.6rem_rgba(243,215,126,0.7)]' : 'bg-white/15'
              }`}
            />
          ))}
        </div>
      </div>

      <GameTimer className="[grid-area:timer]" seconds={remainingSec} fraction={fraction} urgent={urgent} expired={expired} />
    </header>
  )
}
