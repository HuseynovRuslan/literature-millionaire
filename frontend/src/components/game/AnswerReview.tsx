import type { QuizAnswerReview } from '../../types/game'
import { CheckIcon, ClockIcon } from '../home/GameShowArt'

/**
 * The questions of a finished quiz with their correct answers.
 *
 * Shown only on the result screen. During the round nothing is disclosed - that is the whole point of
 * the rule - so this list is the first and only moment a player learns what was right.
 */

/** Correct, wrong, late and unanswered read as four different things: icon + colour + label, never colour alone. */
const TONE = {
  correct: { ring: 'ring-ok/40', badge: 'bg-ok text-ink-950', label: 'Düzgün' },
  wrong: { ring: 'ring-bad/40', badge: 'bg-bad text-ink-950', label: 'Səhv' },
  late: { ring: 'ring-bad/40', badge: 'bg-bad text-ink-950', label: 'Vaxtında deyil' },
  missed: { ring: 'ring-white/15', badge: 'bg-white/20 text-fg', label: 'Vaxt bitdi' },
} as const

function toneOf(item: QuizAnswerReview): keyof typeof TONE {
  if (item.isCorrect) return 'correct'
  if (item.selectedOption === null) return 'missed'
  // Something was picked, but the server clock had already closed the question.
  return item.timedOut ? 'late' : 'wrong'
}

export default function AnswerReview({ items }: { items: QuizAnswerReview[] }) {
  if (items.length === 0) {
    return (
      <p className="py-8 text-center font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-bold text-fg-2">
        Cavabların siyahısı əlçatan deyil
      </p>
    )
  }

  return (
    <ol data-testid="answer-review" className="flex flex-col gap-2.5">
      {items.map((item, index) => {
        const toneKey = toneOf(item)
        const tone = TONE[toneKey]
        return (
          <li
            key={item.questionNumber}
            data-testid="review-item"
            data-correct={item.isCorrect ? 'yes' : 'no'}
            style={{ animationDelay: `${index * 60}ms` }}
            className={`slide-in-right rounded-2xl bg-white/[0.05] px-[clamp(0.8rem,1.1vw,1rem)] py-3 ring-1 ${tone.ring} max-sm:px-3`}
          >
            <div className="flex items-start gap-3">
              <span className={`mt-0.5 grid size-[clamp(1.9rem,2.3vw,2.2rem)] shrink-0 place-items-center rounded-xl font-display text-[clamp(0.8rem,0.95vw,0.9rem)] font-extrabold ${tone.badge}`} aria-hidden="true">
                {toneKey === 'correct' ? <CheckIcon className="size-[60%]" /> : toneKey === 'missed' ? <ClockIcon className="size-[60%]" /> : '✕'}
              </span>
              <div className="min-w-0 flex-1">
                <p lang="az" className="text-[clamp(0.98rem,1.12vw,1.06rem)] font-bold leading-snug text-fg [overflow-wrap:anywhere] max-sm:text-[0.94rem]">
                  <span className="text-fg-3">{item.questionNumber}. </span>
                  {item.text}
                </p>

                <p className="mt-1.5 text-[clamp(0.9rem,1vw,0.96rem)] leading-snug text-fg-2 max-sm:text-[0.88rem]">
                  <span className="font-bold text-ok">Düzgün cavab:</span>{' '}
                  <span lang="az" className="font-semibold text-fg [overflow-wrap:anywhere]">{item.correctOption}) {item.correctAnswer}</span>
                </p>

                {/* A correct answer needs no second line: the row above already is the player's answer. */}
                {!item.isCorrect && (
                  <p className="mt-0.5 text-[clamp(0.9rem,1vw,0.96rem)] leading-snug text-fg-2 max-sm:text-[0.88rem]">
                    <span className="font-bold text-bad">Sizin cavabınız:</span>{' '}
                    {item.selectedOption === null
                      ? <span>cavab verilmədi</span>
                      : (
                        <>
                          <span lang="az" className="font-semibold text-fg [overflow-wrap:anywhere]">{item.selectedOption}) {item.selectedAnswer}</span>
                          {/* A bare "(gec)" told the player nothing; say why it did not count. */}
                          {item.timedOut && <span> — vaxt bitdiyi üçün sayılmadı</span>}
                        </>
                      )}
                  </p>
                )}

                {item.explanation && (
                  <p lang="az" className="mt-1.5 rounded-xl bg-white/[0.04] px-3 py-2 text-[clamp(0.85rem,0.95vw,0.9rem)] leading-snug text-fg-3 [overflow-wrap:anywhere] max-sm:text-[0.84rem]">
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
