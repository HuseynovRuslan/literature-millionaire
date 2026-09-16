import type { AnswerOption } from '../../types/game'

/** Answer-slot colours (tokens in index.css). Paired with AnswerShape so colour is never the only cue. */
export const OPTION_COLOR: Record<AnswerOption, string> = {
  A: 'var(--color-opt-a)',
  B: 'var(--color-opt-b)',
  C: 'var(--color-opt-c)',
  D: 'var(--color-opt-d)',
}
