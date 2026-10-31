/**
 * The home hero's artwork: the violet leather book with its few leaves and gold ornaments, standing in its
 * own light.
 *
 * Decorative - the title beside it says everything it says - so it is hidden from assistive technology
 * and takes no pointer events. It is, however, the largest thing on the first screen and so the page's
 * largest paint: it is fetched at high priority and carries its intrinsic size, so nothing below it
 * jumps when it arrives.
 *
 * The glow and the shadow under the book live on one "stage" sized from the artwork itself (see
 * .hero-art-stage in index.css), so they stay where they belong however wide the column is.
 *
 * There are no floating sparks around it any more. The premium render was briefed against exactly that -
 * no scattered stars, no collectible ornaments - and it carries its own two gold stars; a ring of extra
 * ones drawn by the page pulled it back toward the game look it replaced.
 */
export default function HeroArt() {
  return (
    <div aria-hidden="true" className="hero-art">
      <div className="hero-art-stage">
        <span className="hero-art-glow" />
        <span className="hero-art-floor" />
        <img
          src="/images/bilik-bagi-hero.webp"
          alt=""
          width={1100}
          height={740}
          decoding="async"
          fetchPriority="high"
          draggable={false}
          className="hero-art-img"
        />
      </div>
    </div>
  )
}
