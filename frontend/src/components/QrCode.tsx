import { useMemo } from 'react'
import qr from 'qrcode-generator'

/**
 * A QR code drawn as one SVG path.
 *
 * Rendered as vectors rather than a canvas so it stays sharp on any screen and needs no ref, no
 * pixel ratio handling and no re-draw on resize. The quiet zone is part of the spec, not decoration:
 * scanners need the margin to find the symbol at all.
 */
export default function QrCode({ value, className, title }: { value: string; className?: string; title: string }) {
  const { path, size } = useMemo(() => {
    // Error correction M: readable when part of the code is obscured, without inflating the modules.
    const code = qr(0, 'M')
    code.addData(value)
    code.make()
    const count = code.getModuleCount()
    const quiet = 4
    const parts: string[] = []
    for (let row = 0; row < count; row++) {
      for (let col = 0; col < count; col++) {
        if (code.isDark(row, col)) parts.push(`M${col + quiet} ${row + quiet}h1v1h-1z`)
      }
    }
    return { path: parts.join(''), size: count + quiet * 2 }
  }, [value])

  return (
    <svg
      viewBox={`0 0 ${size} ${size}`}
      className={className}
      role="img"
      aria-label={title}
      data-testid="qr-code"
      shapeRendering="crispEdges"
    >
      <rect width={size} height={size} fill="#ffffff" />
      <path d={path} fill="#000000" />
    </svg>
  )
}
