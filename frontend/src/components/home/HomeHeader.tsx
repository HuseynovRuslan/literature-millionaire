import BrandMark from '../national/BrandMark'
import { PRODUCT_NAME } from '../national/KioskBrand'

/**
 * Home page brand bar: the Bakı Abadlıq Xidməti mark, a gold divider and the product name as the
 * main wordmark. Home-only, so the shared KioskBrand used by the other screens stays unchanged.
 * `uppercase` relies on lang="az" so that "i" becomes "İ".
 */
export default function HomeHeader() {
  return (
    <header className="relative z-10 flex shrink-0 items-center px-[calc(var(--frame)_+_2rem)] pt-[calc(var(--frame)_+_1rem)] max-sm:px-[calc(var(--frame)_+_0.75rem)] max-sm:pt-[calc(var(--frame)_+_0.6rem_+_var(--safe-top))]">
      <div className="flex min-w-0 items-center gap-[clamp(0.9rem,1.3vw,1.17rem)] max-sm:gap-2.5" data-testid="kiosk-brand">
        {/* Phones: if the logo image fails, its text fallback may wrap instead of pushing the wordmark off screen. */}
        <BrandMark className="max-sm:min-w-0 max-sm:shrink" />
        <span
          aria-hidden
          className="h-[clamp(2.8rem,4.2vw,3.78rem)] w-[3px] shrink-0 rounded-full bg-gradient-to-b from-transparent via-[var(--p-gold)] to-transparent max-sm:h-9 max-sm:w-[2px]"
        />
        <p
          lang="az"
          className="shrink-0 whitespace-nowrap font-display text-[clamp(2.1rem,3vw,2.7rem)] font-bold uppercase leading-none tracking-[0.07em] text-[var(--p-burgundy)] max-sm:text-[1.4rem] max-sm:tracking-[0.05em]"
          data-testid="product-name"
        >
          {PRODUCT_NAME}
        </p>
      </div>
    </header>
  )
}
