/** Shared button sizes for the information screens. Colours live in index.css (.btn-primary / .btn-secondary). */

/** The one action a screen is about: large and touch-friendly (>= 80px on the kiosk, 60px on phones). */
export const PRIMARY_CTA =
  'btn btn-primary min-h-[clamp(4.6rem,9vh,6rem)] px-[clamp(2rem,3vw,3.2rem)] text-[clamp(1.25rem,1.9vw,1.9rem)] max-sm:min-h-[3.75rem] max-sm:flex-none max-sm:px-6 max-sm:text-[1.1rem]'

/** Same height as the primary so the row stays tidy, lighter and in a smaller type. */
export const SECONDARY_CTA =
  'btn btn-secondary min-h-[clamp(4.6rem,9vh,6rem)] px-[clamp(1.4rem,2.2vw,2.4rem)] text-[clamp(1rem,1.3vw,1.3rem)] max-sm:min-h-[3.5rem] max-sm:flex-none max-sm:text-[1rem]'

/** Category-card actions: the same touch-target floor (>= 60px kiosk, >= 52px phone), sized for a card grid. */
export const CARD_PRIMARY_CTA =
  'btn btn-primary min-h-[clamp(3.9rem,6.6vh,4.6rem)] px-[clamp(0.9rem,1.3vw,1.4rem)] text-[clamp(0.95rem,1.1vw,1.15rem)] max-sm:min-h-[3.4rem] max-sm:text-[1rem]'

export const CARD_SECONDARY_CTA =
  'btn btn-secondary min-h-[clamp(3.9rem,6.6vh,4.6rem)] px-[clamp(0.7rem,1vw,1.1rem)] text-[clamp(0.82rem,0.95vw,1rem)] max-sm:min-h-[3.25rem] max-sm:text-[0.92rem]'
