import { Octagram } from '../arena/NationalMotifs'

/**
 * The home hero's artwork: the open book with the growing plant and the question mark, standing in its
 * own light.
 *
 * Decorative - the title beside it says everything it says - so it is hidden from assistive technology
 * and takes no pointer events. It is, however, the largest thing on the first screen and so the page's
 * largest paint: it is fetched at high priority and carries its intrinsic size, so nothing below it
 * jumps when it arrives.
 *
 * Everything lives on one "stage" sized from the artwork itself (see .hero-art-stage in index.css), so
 * the glow, the shadow under the book and the sparks stay where they belong around the picture however
 * wide the column around it is - positioned against the column instead, a spark meant to sit beside the
 * book ended up half a screen away from it on a wide monitor.
 *
 * The sparks are the national eight-pointed star the rest of the product already uses (NationalMotifs),
 * not a new icon set. Each drifts on its own period so they never move in step; on a phone the two
 * furthest out are dropped.
 */
const SPARKS = [
  { top: '14%', left: '6%', size: '0.95rem', tone: 'text-sun', far: true, period: '9s', delay: '0s' },
  { top: '4%', left: '70%', size: '0.6rem', tone: 'text-brand-soft', far: false, period: '11s', delay: '-3s' },
  { top: '52%', left: '1%', size: '0.55rem', tone: 'text-brand-soft', far: false, period: '8s', delay: '-5s' },
  { top: '74%', left: '93%', size: '0.8rem', tone: 'text-sun', far: true, period: '12s', delay: '-1.5s' },
  { top: '30%', left: '96%', size: '0.5rem', tone: 'text-white', far: false, period: '10s', delay: '-7s' },
] as const

export default function HeroArt() {
  return (
    <div aria-hidden="true" className="hero-art">
      <div className="hero-art-stage">
        <span className="hero-art-glow" />
        <span className="hero-art-floor" />
        {SPARKS.map((spark, index) => (
          <span
            key={index}
            data-far={spark.far || undefined}
            className={`hero-spark ${spark.tone}`}
            style={{ top: spark.top, left: spark.left, width: spark.size, height: spark.size, animationDuration: spark.period, animationDelay: spark.delay }}
          >
            <Octagram className="size-full" />
          </span>
        ))}
        <img
          src="/images/bilik-bagi-hero.webp"
          alt=""
          width={699}
          height={820}
          decoding="async"
          fetchPriority="high"
          draggable={false}
          className="hero-art-img"
        />
      </div>
    </div>
  )
}
