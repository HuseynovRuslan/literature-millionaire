import type { AnswerOption } from '../types/game'

export type AnswerVisual = 'idle' | 'selected' | 'correct' | 'wrong' | 'dimmed'

interface Props {
  option: AnswerOption
  text: string
  visual: AnswerVisual
  disabled: boolean
  onSelect: (option: AnswerOption) => void
}

// Paper theme (see `.paper` in index.css). During the quiz only idle / selected / dimmed are
// used, so no colour here can reveal correctness; correct / wrong stay for any future reveal screen.
const visuals: Record<AnswerVisual, string> = {
  idle: 'border-[var(--p-gold-light)] bg-white text-[var(--p-ink)] shadow-[var(--p-shadow)]',
  selected: 'border-[var(--p-gold)] bg-[var(--p-burgundy)] text-[var(--p-paper)] shadow-[0_0_0_3px_rgba(207,156,60,0.45)]',
  correct: 'border-[#189a68] bg-[#e6f5ee] text-[var(--p-ink)]',
  wrong: 'border-[#b32a31] bg-[#f9e6e7] text-[var(--p-ink)]',
  dimmed: 'border-[var(--p-line)] bg-[var(--p-paper-2)] text-[var(--p-ink-2)] opacity-80',
}

const badges: Record<AnswerVisual, string> = {
  idle: 'border-[var(--p-burgundy)] bg-white text-[var(--p-burgundy)]',
  selected: 'border-[var(--p-gold-light)] bg-[var(--p-gold-light)] text-[var(--p-burgundy)]',
  correct: 'border-[#189a68] bg-[#189a68] text-white',
  wrong: 'border-[#b32a31] bg-[#b32a31] text-white',
  dimmed: 'border-[var(--p-line)] bg-white text-[var(--p-ink-2)]',
}

export default function AnswerButton({ option, text, visual, disabled, onSelect }: Props) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => onSelect(option)}
      aria-pressed={visual === 'selected'}
      className={[
        'tap flex w-full items-center gap-5 rounded-2xl border-2 px-6 text-left max-sm:flex-col max-sm:justify-center max-sm:gap-1.5 max-sm:rounded-xl max-sm:px-2 max-sm:text-center',
        'min-h-[5.5rem] py-4 lg:min-h-[7rem] max-sm:min-h-[4.5rem] max-sm:py-2',
        'text-[clamp(1.1rem,1.6vw,1.75rem)] font-medium leading-snug max-sm:text-[0.95rem] max-sm:leading-tight',
        'disabled:cursor-default',
        visuals[visual],
      ].join(' ')}
    >
      <span
        className={[
          'flex h-14 w-14 shrink-0 rotate-45 items-center justify-center rounded-md border-2 max-sm:my-1 max-sm:h-6 max-sm:w-6 max-sm:rounded',
          badges[visual],
        ].join(' ')}
        aria-hidden
      >
        <span className="-rotate-45 font-display text-[1.9rem] font-bold leading-none max-sm:text-[0.95rem]">{option}</span>
      </span>
      <span className="min-w-0 flex-1 break-words max-sm:w-full max-sm:flex-none [overflow-wrap:anywhere] [hyphens:auto]" lang="az">
        {text}
      </span>
    </button>
  )
}
