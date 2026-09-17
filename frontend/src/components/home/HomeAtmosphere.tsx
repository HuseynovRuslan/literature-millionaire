/**
 * The home page's background atmosphere: a faint national pattern, soft ambient light and a sparse star
 * field, layered between the page's base colour and its content.
 *
 * Opt-in (GameShowShell's `atmosphere`), because the arena background is shared by every information
 * screen and the game itself; this is for the home page only.
 *
 * Cheap on purpose. It is fixed to the viewport, so a long page scrolls over it instead of repainting a
 * three-thousand-pixel gradient, and it is nine elements in all: the eighteen still stars are the shadows
 * of a single dot (.atmo-stars in index.css), and only the five that twinkle are nodes of their own.
 * Entirely decorative - hidden from assistive technology and transparent to every pointer.
 */
const TWINKLES = [
  { top: '13%', left: '31%', size: '3px', tone: 'rgba(255, 255, 255, 0.95)', period: '4.2s', delay: '0s', phone: true },
  { top: '27%', left: '88%', size: '2px', tone: 'rgba(201, 188, 255, 0.95)', period: '5.6s', delay: '-2.1s', phone: false },
  { top: '58%', left: '6%', size: '2px', tone: 'rgba(140, 225, 255, 0.9)', period: '3.4s', delay: '-1.2s', phone: true },
  { top: '71%', left: '63%', size: '3px', tone: 'rgba(201, 188, 255, 0.9)', period: '6s', delay: '-3.7s', phone: false },
  { top: '89%', left: '22%', size: '2px', tone: 'rgba(255, 255, 255, 0.9)', period: '4.8s', delay: '-0.6s', phone: true },
] as const

export default function HomeAtmosphere() {
  return (
    <div aria-hidden="true" className="atmo">
      <div className="atmo-pattern" />
      <div className="atmo-glow" />
      <div className="atmo-stars" />
      {TWINKLES.map((star, index) => (
        <span
          key={index}
          data-phone={star.phone || undefined}
          className="atmo-twinkle"
          style={{
            top: star.top,
            left: star.left,
            width: star.size,
            height: star.size,
            color: star.tone,
            animationDuration: star.period,
            animationDelay: star.delay,
          }}
        />
      ))}
    </div>
  )
}
