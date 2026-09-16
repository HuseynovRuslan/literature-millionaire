/** Thin gold rule with a single eight-pointed star: the one ornament we repeat. */
export default function GoldRule({ className = '' }: { className?: string }) {
  return (
    <div className={`flex items-center gap-4 text-gold ${className}`} aria-hidden>
      <span className="h-px flex-1 bg-gradient-to-r from-transparent to-gold/70" />
      <svg width="22" height="22" viewBox="0 0 22 22" fill="none" stroke="currentColor" strokeWidth="1.2">
        <rect x="5" y="5" width="12" height="12" />
        <rect x="5" y="5" width="12" height="12" transform="rotate(45 11 11)" />
      </svg>
      <span className="h-px flex-1 bg-gradient-to-l from-transparent to-gold/70" />
    </div>
  )
}
