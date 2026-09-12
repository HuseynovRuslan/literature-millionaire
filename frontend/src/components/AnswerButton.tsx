import type { AnswerOption } from '../types/game'

export type AnswerVisual = 'idle' | 'selected' | 'correct' | 'wrong' | 'dimmed'

interface Props {
  option: AnswerOption
  text: string
  visual: AnswerVisual
  disabled: boolean
  onSelect: (option: AnswerOption) => void
}

const visuals: Record<AnswerVisual, string> = {
  idle: 'border-navy-600 bg-navy-700/80 text-ivory',
  selected: 'border-gold bg-gold/15 text-ivory shadow-[0_0_0_3px_rgba(212,168,59,0.45)]',
  correct: 'border-ok bg-ok/20 text-ivory shadow-[0_0_0_3px_rgba(47,182,122,0.5)]',
  wrong: 'border-bad bg-bad/20 text-ivory shadow-[0_0_0_3px_rgba(224,78,94,0.5)]',
  dimmed: 'border-navy-600/50 bg-navy-800/50 text-mist/70',
}

const badges: Record<AnswerVisual, string> = {
  idle: 'border-gold/70 text-gold',
  selected: 'border-gold bg-gold text-navy-900',
  correct: 'border-ok bg-ok text-navy-900',
  wrong: 'border-bad bg-bad text-ivory',
  dimmed: 'border-mist/40 text-mist/60',
}

export default function AnswerButton({ option, text, visual, disabled, onSelect }: Props) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => onSelect(option)}
      aria-pressed={visual === 'selected'}
      className={[
        'tap flex w-full items-center gap-5 rounded-2xl border-2 px-6 text-left',
        'min-h-[5.5rem] py-4 lg:min-h-[7rem]',
        'text-[clamp(1.1rem,1.6vw,1.75rem)] font-medium leading-snug',
        'disabled:cursor-default',
        visuals[visual],
      ].join(' ')}
    >
      <span
        className={[
          'flex h-14 w-14 shrink-0 items-center justify-center rounded-full border-2',
          'font-display text-[1.9rem] font-bold leading-none',
          badges[visual],
        ].join(' ')}
        aria-hidden
      >
        {option}
      </span>
      <span className="min-w-0 flex-1 break-words [overflow-wrap:anywhere] [hyphens:auto]" lang="az">
        {text}
      </span>
    </button>
  )
}
