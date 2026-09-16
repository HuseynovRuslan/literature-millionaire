import type { QuizAnswerReview } from '../../types/game'

/**
 * The 10 questions of a finished quiz with their correct answers.
 *
 * Shown only on the result screen. During the round nothing is disclosed - that is the whole point of
 * the rule - so this list is the first and only moment a player learns what was right.
 */

/** Correct, wrong and unanswered read as three different things, not just two colours. */
const TONE = {
  correct: { ring: 'ring-[rgba(30,167,156,0.55)]', badge: 'bg-[rgba(30,167,156,0.9)] text-[#04201e]', label: 'Düzgün' },
  wrong: { ring: 'ring-[rgba(255,154,162,0.45)]', badge: 'bg-[rgba(125,22,29,0.85)] text-[#ffe8ea]', label: 'Səhv' },
  missed: { ring: 'ring-white/15', badge: 'bg-white/20 text-[#e6ecf7]', label: 'Vaxt bitdi' },
} as const

function toneOf(item: QuizAnswerReview): keyof typeof TONE {
  if (item.isCorrect) return 'correct'
  return item.selectedOption === null ? 'missed' : 'wrong'
}

export default function AnswerReview({ items }: { items: QuizAnswerReview[] }) {
  if (items.length === 0) {
    return (
      <p className="py-[clamp(1rem,3vh,2rem)] text-center font-display text-[clamp(1.2rem,1.7vw,1.9rem)] font-semibold text-[#d6deec]">
        Cavabların siyahısı əlçatan deyil
      </p>
    )
  }

  return (
    <ol data-testid="answer-review" className="flex flex-col gap-[clamp(0.4rem,0.9vh,0.7rem)]">
      {items.map((item) => {
        const tone = TONE[toneOf(item)]
        return (
          <li
            key={item.questionNumber}
            data-testid="review-item"
            data-correct={item.isCorrect ? 'yes' : 'no'}
            className={`rounded-xl bg-white/[0.07] px-[clamp(0.7rem,1vw,1.1rem)] py-[clamp(0.45rem,1vh,0.8rem)] ring-1 ${tone.ring} max-sm:px-2.5`}
          >
            <div className="flex items-start gap-[clamp(0.55rem,0.9vw,1rem)]">
              <span className={`mt-[0.15em] grid size-[clamp(1.6rem,2vw,2.1rem)] shrink-0 place-items-center rounded-full font-display text-[clamp(0.85rem,1vw,1.1rem)] font-bold tabular-nums ${tone.badge}`}>
                {item.questionNumber}
              </span>
              <div className="min-w-0 flex-1">
                <p lang="az" className="text-[clamp(0.95rem,1.15vw,1.25rem)] font-semibold leading-snug text-[#fbf6ec] [overflow-wrap:anywhere] max-sm:text-[0.92rem]">
                  {item.text}
                </p>

                <p className="mt-[0.35em] text-[clamp(0.85rem,1.02vw,1.1rem)] leading-snug text-[#c9d3e6] max-sm:text-[0.85rem]">
                  <span className="font-semibold text-[var(--p-teal)]">Düzgün cavab:</span>{' '}
                  <span lang="az" className="text-[#fbf6ec] [overflow-wrap:anywhere]">{item.correctOption}) {item.correctAnswer}</span>
                </p>

                {/* A correct answer needs no second line: the row above already is the player's answer. */}
                {!item.isCorrect && (
                  <p className="text-[clamp(0.85rem,1.02vw,1.1rem)] leading-snug text-[#c9d3e6] max-sm:text-[0.85rem]">
                    <span className="font-semibold text-[#ff9aa2]">Sizin cavabınız:</span>{' '}
                    {item.selectedOption === null
                      ? <span className="text-[#d6deec]">cavab verilmədi</span>
                      : <span lang="az" className="text-[#fbf6ec] [overflow-wrap:anywhere]">{item.selectedOption}) {item.selectedAnswer}{item.timedOut && ' (gec)'}</span>}
                  </p>
                )}

                {item.explanation && (
                  <p lang="az" className="mt-[0.3em] text-[clamp(0.8rem,0.95vw,1.02rem)] leading-snug text-[#a9b6cc] [overflow-wrap:anywhere] max-sm:text-[0.8rem]">
                    {item.explanation}
                  </p>
                )}
              </div>
              <span className="sr-only">{tone.label}</span>
            </div>
          </li>
        )
      })}
    </ol>
  )
}
