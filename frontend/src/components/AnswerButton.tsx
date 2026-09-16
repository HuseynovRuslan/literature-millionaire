import type { CSSProperties } from 'react'
import type { AnswerOption } from '../types/game'
import AnswerShape from './arena/AnswerShape'
import { OPTION_COLOR } from './arena/optionColors'
import { LockIcon } from './home/GameShowArt'

export type AnswerVisual = 'idle' | 'selected' | 'correct' | 'wrong' | 'dimmed'

interface Props {
  option: AnswerOption
  text: string
  visual: AnswerVisual
  disabled: boolean
  onSelect: (option: AnswerOption) => void
}

// Colour and shape belong to the letter slot, which the server shuffles per session, so they say nothing
// about correctness. During the quiz only idle / selected / dimmed are used; `correct` / `wrong` have no
// special styling (see .answer in index.css), so no state can reveal an answer.
export default function AnswerButton({ option, text, visual, disabled, onSelect }: Props) {
  const selected = visual === 'selected'
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => onSelect(option)}
      aria-pressed={selected}
      aria-label={`${option}: ${text}`}
      data-option={option}
      data-visual={visual}
      style={{ '--opt': OPTION_COLOR[option] } as CSSProperties}
      className="answer flex min-h-[clamp(5rem,11vh,8rem)] w-full min-w-0 items-center gap-[clamp(0.8rem,1.3vw,1.4rem)] rounded-[1.4rem] px-[clamp(0.9rem,1.4vw,1.5rem)] py-[clamp(0.6rem,1.2vh,1rem)] text-left disabled:cursor-default max-sm:min-h-[4.5rem] max-sm:flex-col max-sm:justify-center max-sm:gap-1.5 max-sm:rounded-2xl max-sm:px-2 max-sm:py-2 max-sm:text-center"
    >
      <span aria-hidden="true" className="answer-shape relative grid size-[clamp(3rem,min(4.2vw,7vh),4.4rem)] shrink-0 place-items-center rounded-2xl max-sm:size-8 max-sm:rounded-lg">
        <AnswerShape option={option} className="size-[52%] text-white drop-shadow-[0_2px_0_rgba(0,0,0,0.25)]" />
        <span className="absolute -bottom-1.5 -right-1.5 grid size-[clamp(1.4rem,1.8vw,1.8rem)] place-items-center rounded-full bg-white font-display text-[clamp(0.7rem,0.9vw,0.9rem)] font-extrabold text-ink-900 max-sm:-bottom-1 max-sm:-right-2 max-sm:size-[1.1rem] max-sm:text-[0.58rem]">
          {option}
        </span>
      </span>
      <span
        data-answer-text
        lang="az"
        className="min-w-0 flex-1 font-sans text-[clamp(1.15rem,min(1.75vw,3.1vh),2rem)] font-extrabold leading-snug text-white [hyphens:auto] [overflow-wrap:anywhere] [text-shadow:0_1px_1px_rgba(0,0,0,0.25)] max-sm:w-full max-sm:flex-none max-sm:text-[0.92rem] max-sm:leading-tight max-sm:[overflow-wrap:break-word]"
      >
        {text}
      </span>
      {selected && (
        <span className="pop grid size-[clamp(2.2rem,2.8vw,2.8rem)] shrink-0 place-items-center rounded-full bg-white text-ink-900 max-sm:absolute max-sm:right-1.5 max-sm:top-1.5 max-sm:size-6" aria-hidden="true">
          <LockIcon className="size-[55%]" />
        </span>
      )}
    </button>
  )
}
