import { useState } from 'react'

export const BRAND_NAME = 'Bakı Abadlıq Xidməti MMC'

/**
 * Organisation mark for the kiosk. Shows public/brand/bakiabadliq.jpg when it loads;
 * otherwise the organisation name in text. The image's provenance is documented in
 * public/brand/README.txt and has not been verified as the official approved logo.
 * `sm` is the compact size for screens whose header row is shared with other content.
 */
export default function BrandMark({ className = '', size = 'md' }: { className?: string; size?: 'sm' | 'md' }) {
  const [failed, setFailed] = useState(false)
  const small = size === 'sm'
  return (
    <div
      className={`flex shrink-0 items-center gap-3 rounded-xl border-2 border-[var(--p-gold-light,#e9c069)] bg-white shadow-[var(--p-shadow)] ${small ? 'px-2 py-1.5 max-sm:px-1.5 max-sm:py-1' : 'px-3 py-2 max-sm:px-2 max-sm:py-1.5'} ${className}`}
      data-testid="brand"
    >
      {failed ? (
        <span
          className={`font-display font-semibold leading-tight text-[var(--p-burgundy,#7d161d)] ${small ? 'text-[clamp(0.9rem,1.1vw,1.15rem)]' : 'text-[clamp(1rem,1.3vw,1.3rem)]'}`}
          data-testid="brand-fallback"
        >
          {BRAND_NAME}
        </span>
      ) : (
        <img
          src="/brand/bakiabadliq.jpg"
          alt={BRAND_NAME}
          onError={() => setFailed(true)}
          className={small ? 'h-[clamp(2.6rem,4vw,3.9rem)] w-auto max-sm:h-8' : 'h-[clamp(3rem,5vw,5.2rem)] w-auto max-sm:h-11'}
          draggable={false}
        />
      )}
    </div>
  )
}
