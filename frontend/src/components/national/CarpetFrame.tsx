import { Gol, Octagram } from './Ornaments'

/**
 * Thin carpet border around the viewport: four pattern strips and four corner stars.
 * Pattern geometry adapted from the cingiz branch; sized by the `--frame` variable on `.paper`.
 * Pointer events pass through so it never blocks touch targets.
 */
export default function CarpetFrame() {
  const strip = 'absolute pointer-events-none'
  return (
    <div className="pointer-events-none absolute inset-0 z-10" aria-hidden="true">
      <svg className="absolute h-0 w-0">
        <defs>
          <pattern id="carpet-h" width="46" height="26" patternUnits="userSpaceOnUse">
            <rect width="46" height="26" fill="var(--p-paper)" />
            <rect y="1" width="46" height="1.6" fill="var(--p-gold)" />
            <rect y="23.4" width="46" height="1.6" fill="var(--p-gold)" />
            <path d="M23 5 L32 13 L23 21 L14 13 Z" fill="none" stroke="var(--p-burgundy)" strokeWidth="1.6" />
            <path d="M23 9.5 L26.5 13 L23 16.5 L19.5 13 Z" fill="var(--p-teal)" />
            <path d="M0 8 L5 13 L0 18 Z" fill="var(--p-gold)" />
            <path d="M46 8 L41 13 L46 18 Z" fill="var(--p-gold)" />
          </pattern>
          <pattern id="carpet-v" width="26" height="46" patternUnits="userSpaceOnUse">
            <rect width="26" height="46" fill="var(--p-paper)" />
            <rect x="1" width="1.6" height="46" fill="var(--p-gold)" />
            <rect x="23.4" width="1.6" height="46" fill="var(--p-gold)" />
            <path d="M13 14 L21 23 L13 32 L5 23 Z" fill="none" stroke="var(--p-burgundy)" strokeWidth="1.6" />
            <path d="M13 19.5 L16.5 23 L13 26.5 L9.5 23 Z" fill="var(--p-teal)" />
            <path d="M8 0 L13 5 L18 0 Z" fill="var(--p-gold)" />
            <path d="M8 46 L13 41 L18 46 Z" fill="var(--p-gold)" />
          </pattern>
        </defs>
      </svg>

      <svg className={`${strip} left-0 top-0 w-full`} style={{ height: 'var(--frame)' }} preserveAspectRatio="none">
        <rect width="100%" height="100%" fill="url(#carpet-h)" />
      </svg>
      <svg className={`${strip} bottom-0 left-0 w-full`} style={{ height: 'var(--frame)' }} preserveAspectRatio="none">
        <rect width="100%" height="100%" fill="url(#carpet-h)" />
      </svg>
      <svg className={`${strip} left-0 top-0 h-full`} style={{ width: 'var(--frame)' }} preserveAspectRatio="none">
        <rect width="100%" height="100%" fill="url(#carpet-v)" />
      </svg>
      <svg className={`${strip} right-0 top-0 h-full`} style={{ width: 'var(--frame)' }} preserveAspectRatio="none">
        <rect width="100%" height="100%" fill="url(#carpet-v)" />
      </svg>

      {(['left-0 top-0', 'right-0 top-0', 'bottom-0 left-0', 'bottom-0 right-0'] as const).map((pos) => (
        <div
          key={pos}
          className={`absolute grid place-items-center ${pos}`}
          style={{ width: 'calc(var(--frame) * 1.9)', height: 'calc(var(--frame) * 1.9)' }}
        >
          <Octagram className="col-start-1 row-start-1 h-full w-full" inner={false} />
          <Gol className="col-start-1 row-start-1 h-[42%] w-[42%] opacity-80" />
        </div>
      ))}
    </div>
  )
}
