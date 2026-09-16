import type { CSSProperties } from 'react'
import type { AnswerOption } from '../types/game'

export type AnswerVisual = 'idle' | 'selected' | 'correct' | 'wrong' | 'dimmed'

interface Props {
  option: AnswerOption
  text: string
  visual: AnswerVisual
  disabled: boolean
  onSelect: (option: AnswerOption) => void
}

// Brand accents only tell the four options apart (burgundy, blue, teal, amber): no green/red, and the colour
// belongs to the letter slot, which the server shuffles per session, so it says nothing about correctness.
// During the quiz only idle / selected / dimmed are used; `correct` / `wrong` have no special styling here
// (see .gs-answer in index.css), so no state can reveal an answer.
const ACCENT: Record<AnswerOption, string> = { A: '#b8323c', B: '#2f6bd1', C: '#159a8f', D: '#c98a1c' }

export default function AnswerButton({ option, text, visual, disabled, onSelect }: Props) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => onSelect(option)}
      aria-pressed={visual === 'selected'}
      aria-label={`${option}: ${text}`}
      data-option={option}
      data-visual={visual}
      style={{ '--accent': ACCENT[option] } as CSSProperties}
      className="tap gs-answer flex min-h-[clamp(5.5rem,11vh,8.5rem)] w-full min-w-0 items-center gap-[clamp(0.9rem,1.4vw,1.6rem)] rounded-2xl px-[clamp(1rem,1.6vw,1.8rem)] py-[clamp(0.6rem,1.2vh,1.1rem)] text-left disabled:cursor-default max-sm:min-h-[3.625rem] max-sm:flex-col max-sm:justify-center max-sm:gap-1 max-sm:rounded-xl max-sm:px-2 max-sm:py-1.5 max-sm:text-center"
    >
      <span aria-hidden="true" className="gs-answer-badge grid size-[clamp(3rem,min(4vw,6.6vh),4.6rem)] shrink-0 rotate-45 place-items-center rounded-lg max-sm:my-0.5 max-sm:size-6 max-sm:rounded">
        <span className="-rotate-45 font-display text-[clamp(1.8rem,min(2.5vw,4.2vh),2.8rem)] font-bold leading-none text-[#fbf6ec] max-sm:text-[0.95rem]">{option}</span>
      </span>
      <span
        data-answer-text
        lang="az"
        className="min-w-0 flex-1 font-sans text-[clamp(1.25rem,min(1.8vw,3.2vh),2.1rem)] font-semibold leading-snug text-[#fbf6ec] [hyphens:auto] [overflow-wrap:anywhere] max-sm:w-full max-sm:flex-none max-sm:text-[0.95rem] max-sm:leading-tight"
      >
        {text}
      </span>
    </button>
  )
}
