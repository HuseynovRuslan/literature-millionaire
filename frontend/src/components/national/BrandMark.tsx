import { useState } from 'react'

const BRAND_NAME = 'Bakı Abadlıq Xidməti MMC'

/**
 * Organisation mark for the kiosk. Shows public/brand/bakiabadliq.jpg when it loads;
 * otherwise the organisation name in text. The image's provenance is documented in
 * public/brand/README.txt and has not been verified as the official approved logo.
 */
export default function BrandMark({ className = '' }: { className?: string }) {
  const [failed, setFailed] = useState(false)
  return (
    <div
      className={`flex items-center gap-3 rounded-xl border-2 border-[var(--p-gold-light)] bg-white px-3 py-2 shadow-[var(--p-shadow)] ${className}`}
      data-testid="brand"
    >
      {failed ? (
        <span className="font-display text-[clamp(1rem,1.3vw,1.3rem)] font-semibold text-[var(--p-burgundy)]">{BRAND_NAME}</span>
      ) : (
        <img
          src="/brand/bakiabadliq.jpg"
          alt={BRAND_NAME}
          onError={() => setFailed(true)}
          className="h-[clamp(3rem,5vw,5.2rem)] w-auto"
          draggable={false}
        />
      )}
    </div>
  )
}
