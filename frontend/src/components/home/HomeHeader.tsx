import BrandMark from '../national/BrandMark'
import { PRODUCT_NAME } from '../national/KioskBrand'

/** Brand bar for the information screens: organisation logo, product wordmark and the product type. */
export default function HomeHeader() {
  return (
    <header className="relative z-10 mx-auto flex w-full max-w-[112rem] shrink-0 items-center gap-4 px-[clamp(1.2rem,3vw,3rem)] pt-[clamp(1rem,2.4vh,1.8rem)] max-sm:px-4 max-sm:pt-[calc(0.8rem_+_var(--safe-top))]">
      <div className="flex min-w-0 items-center gap-[clamp(0.8rem,1.2vw,1.2rem)] max-sm:gap-2.5" data-testid="kiosk-brand">
        <BrandMark className="max-sm:min-w-0 max-sm:shrink" />
        <div className="min-w-0">
          <p lang="az" className="whitespace-nowrap font-display text-[clamp(1.4rem,2.2vw,2.2rem)] font-extrabold uppercase leading-none tracking-tight max-sm:text-[1.1rem]" data-testid="product-name">
            {PRODUCT_NAME}
          </p>
          <p className="mt-1 text-[clamp(0.72rem,0.9vw,0.9rem)] font-bold uppercase tracking-[0.18em] text-fg-3 max-sm:text-[0.62rem] max-sm:tracking-[0.12em]">
            Bilik yarışı
          </p>
        </div>
      </div>
    </header>
  )
}
