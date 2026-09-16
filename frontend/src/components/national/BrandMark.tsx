import { useState } from 'react'

export const BRAND_NAME = 'Bakı Abadlıq Xidməti MMC'

/**
 * Organisation mark. Shows public/brand/bakiabadliq.jpg when it loads, otherwise the organisation name.
 * The logo has a white background, so it always sits on a white tile. `sm` is the compact header size.
 */
export default function BrandMark({ className = '', size = 'md' }: { className?: string; size?: 'sm' | 'md' }) {
  const [failed, setFailed] = useState(false)
  const small = size === 'sm'
  return (
    <div
      className={`flex shrink-0 items-center rounded-2xl bg-white shadow-[0_6px_0_rgba(123,97,255,0.35),0_12px_24px_-10px_rgba(4,2,18,0.9)] ${small ? 'px-1.5 py-1 max-sm:rounded-xl' : 'px-2.5 py-1.5 max-sm:rounded-xl max-sm:px-1.5 max-sm:py-1'} ${className}`}
      data-testid="brand"
    >
      {failed ? (
        <span className="px-1 font-sans text-sm font-extrabold leading-tight text-ink-800" data-testid="brand-fallback">
          {BRAND_NAME}
        </span>
      ) : (
        <img
          src="/brand/bakiabadliq.jpg"
          alt={BRAND_NAME}
          onError={() => setFailed(true)}
          className={small ? 'h-[clamp(2.3rem,3.4vw,3.2rem)] w-auto max-sm:h-8' : 'h-[clamp(2.8rem,4.4vw,4.4rem)] w-auto max-sm:h-10'}
          draggable={false}
        />
      )}
    </div>
  )
}
