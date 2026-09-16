/** Shared button styles for the game-show screens (home, registration). */

/** The one action a screen is about: large, burgundy, touch-friendly (>= 88px on the kiosk, 60px on phones). */
export const PRIMARY_CTA =
  'tap paper-cta flex min-h-[clamp(5.5rem,11vh,7.5rem)] items-center justify-center gap-[clamp(0.8rem,1.2vw,1.2rem)] rounded-full px-[clamp(2rem,3vw,3.5rem)] font-display text-[clamp(2.1rem,3.1vw,3.6rem)] font-bold tracking-[0.06em] max-sm:min-h-[3.75rem] max-sm:flex-none max-sm:px-6 max-sm:text-[1.65rem]'

/** Same height as the primary so the row stays tidy, but narrower, lighter and in a smaller type. */
export const SECONDARY_CTA =
  'tap paper-ghost flex min-h-[clamp(5.5rem,11vh,7.5rem)] items-center justify-center rounded-full px-[clamp(1.5rem,2.2vw,2.5rem)] font-display text-[clamp(1.3rem,1.75vw,2rem)] font-semibold tracking-[0.05em] max-sm:min-h-[3.25rem] max-sm:flex-none max-sm:text-[1.2rem]'

/**
 * Category-card actions (Task 15B): the same touch-target floor as the full-page CTAs above
 * (>=72px on the kiosk, >=52px on a phone) but sized to sit two-per-card in a grid of up to four.
 */
export const CARD_PRIMARY_CTA =
  'tap paper-cta flex min-h-[clamp(4.6rem,7.6vh,5.2rem)] items-center justify-center rounded-full px-[clamp(0.9rem,1.3vw,1.5rem)] text-center font-display text-[clamp(1rem,1.2vw,1.25rem)] font-bold leading-tight tracking-[0.03em] max-sm:min-h-[3.4rem] max-sm:text-[1.05rem]'

export const CARD_SECONDARY_CTA =
  'tap paper-ghost flex min-h-[clamp(4.6rem,7.6vh,5.2rem)] items-center justify-center rounded-full px-[clamp(0.7rem,1vw,1.2rem)] text-center font-display text-[clamp(0.9rem,1.02vw,1.05rem)] font-semibold leading-tight tracking-[0.02em] max-sm:min-h-[3.25rem] max-sm:text-[0.92rem]'
