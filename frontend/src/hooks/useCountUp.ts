import { useEffect, useState } from 'react'

/** Counts from 0 up to `target` after `delayMs`, easing out. Shows the value straight away with reduced motion. */
export function useCountUp(target: number, durationMs = 1100, delayMs = 0): number {
  const [value, setValue] = useState(0)
  const instant = target <= 0 || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true

  useEffect(() => {
    if (instant) return
    let frame = 0
    let start = 0
    const tick = (now: number) => {
      if (!start) start = now
      const t = Math.min(1, (now - start) / durationMs)
      setValue(Math.round(target * (1 - Math.pow(1 - t, 3))))
      if (t < 1) frame = requestAnimationFrame(tick)
    }
    const timeout = window.setTimeout(() => { frame = requestAnimationFrame(tick) }, delayMs)
    return () => {
      window.clearTimeout(timeout)
      cancelAnimationFrame(frame)
    }
  }, [instant, target, durationMs, delayMs])

  return instant ? target : value
}
