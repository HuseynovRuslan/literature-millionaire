import BrandMark from './BrandMark'

export const PRODUCT_NAME = 'Kitabxana 2.0'

/**
 * Product and organisation identity shown on every kiosk screen: the Bakı Abadlıq Xidməti
 * mark next to the product name. Visual hierarchy on each screen stays
 * product ("Kitabxana 2.0") > campaign format ("Ayın kitabı") > current book.
 */
export default function KioskBrand({
  compact = false,
  tone = 'paper',
  className = '',
}: {
  compact?: boolean
  tone?: 'paper' | 'dark'
  className?: string
}) {
  return (
    <div className={`flex min-w-0 items-center gap-[clamp(0.6rem,1vw,1.1rem)] ${className}`} data-testid="kiosk-brand">
      <BrandMark size={compact ? 'sm' : 'md'} />
      <p
        className={[
          'whitespace-nowrap font-display font-bold leading-none',
          compact ? 'text-[clamp(1.3rem,1.9vw,2.2rem)]' : 'text-[clamp(1.5rem,2.3vw,2.7rem)]',
          tone === 'dark' ? 'text-ivory' : 'text-[var(--p-burgundy)]',
        ].join(' ')}
        data-testid="product-name"
      >
        {PRODUCT_NAME}
      </p>
    </div>
  )
}

/** Top header row for the paper screens whose content is centered below it. */
export function KioskHeader() {
  return (
    <header className="relative z-10 flex shrink-0 items-center" style={{ padding: 'calc(var(--frame) + 0.8rem) calc(var(--frame) + 1.5rem) 0' }}>
      <KioskBrand />
    </header>
  )
}
