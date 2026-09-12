// Milli naxış elementləri: buta, göl (medalyon), səkkizguşəli ulduz,
// açıq fon və onun üzərinə səpələnmiş naxışlar, incə xalça haşiyəsi.

export const BUTA_D =
  'M50 135 C18 135 5 105 12 78 C18 52 38 38 48 22 C54 12 62 4 74 6 C86 8 88 22 80 26 C92 48 96 78 88 102 C82 122 68 135 50 135 Z'
const BUTA_MID_D =
  'M50 124 C27 124 17 103 22 83 C27 63 42 52 52 38 C58 30 64 24 70 22 C80 44 84 70 79 94 C75 112 64 124 50 124 Z'
const BUTA_IN_D = 'M50 113 C34 113 28 99 31 86 C34 72 45 63 54 52 C62 62 70 78 69 92 C68 105 60 113 50 113 Z'

export function Buta({
  color = 'var(--gold-500)',
  accent = 'var(--red-700)',
  core = 'var(--teal-500)',
  className = '',
  style,
  flip = false,
}) {
  return (
    <svg
      viewBox="0 0 100 140"
      className={className}
      style={{ transform: flip ? 'scaleX(-1)' : undefined, ...style }}
      aria-hidden="true"
    >
      <path d={BUTA_D} fill={color} />
      <path d={BUTA_MID_D} fill={accent} />
      <path d={BUTA_IN_D} fill={color} />
      <circle cx="50" cy="92" r="10" fill={accent} />
      <circle cx="50" cy="92" r="6" fill={core} />
      <circle cx="50" cy="92" r="2.4" fill={color} />
      {[0, 45, 90, 135, 180, 225, 270, 315].map((a) => (
        <circle
          key={a}
          cx={50 + 15 * Math.cos((a * Math.PI) / 180)}
          cy={92 + 15 * Math.sin((a * Math.PI) / 180)}
          r="2.2"
          fill={accent}
        />
      ))}
      <path d="M62 46 C68 52 72 60 73 68" stroke={core} strokeWidth="2.4" fill="none" strokeLinecap="round" />
      <circle cx="77" cy="15" r="3.4" fill={core} />
    </svg>
  )
}

// Yalnız cizgi ilə çəkilmiş buta — açıq fonda səpələnmiş naxışlar üçün
export function ButaOutline({ className = '', stroke = 'currentColor', style, flip = false }) {
  return (
    <svg
      viewBox="0 0 100 140"
      className={className}
      style={{ transform: flip ? 'scaleX(-1)' : undefined, ...style }}
      aria-hidden="true"
      fill="none"
      stroke={stroke}
      strokeWidth="3"
      strokeLinejoin="round"
    >
      <path d={BUTA_D} />
      <path d={BUTA_IN_D} strokeWidth="2.4" />
      <circle cx="50" cy="92" r="8" strokeWidth="2.4" />
      <circle cx="50" cy="92" r="2.6" strokeWidth="2.4" />
    </svg>
  )
}

// Səkkizguşəli ulduz — kaşı (şəbəkə) motivi
export function Octagram({ className = '', fill = 'var(--indigo-900)', stroke = 'var(--gold-500)', inner = true, style }) {
  return (
    <svg viewBox="0 0 100 100" className={className} style={style} aria-hidden="true">
      <g fill={fill} stroke={stroke} strokeWidth="2.5" strokeLinejoin="round">
        <rect x="17" y="17" width="66" height="66" />
        <rect x="17" y="17" width="66" height="66" transform="rotate(45 50 50)" />
      </g>
      {inner && (
        <>
          <circle cx="50" cy="50" r="31" fill={fill} stroke={stroke} strokeWidth="1.2" strokeOpacity="0.55" />
          <circle cx="50" cy="50" r="26" fill="none" stroke={stroke} strokeWidth="1" strokeOpacity="0.35" />
        </>
      )}
    </svg>
  )
}

// Cizgili ulduz
export function OctagramOutline({ className = '', stroke = 'currentColor', style }) {
  return (
    <svg viewBox="0 0 100 100" className={className} style={style} aria-hidden="true" fill="none" stroke={stroke}>
      <g strokeWidth="3" strokeLinejoin="round">
        <rect x="17" y="17" width="66" height="66" />
        <rect x="17" y="17" width="66" height="66" transform="rotate(45 50 50)" />
      </g>
      <circle cx="50" cy="50" r="18" strokeWidth="2.4" />
    </svg>
  )
}

// Xalça gölü (romb medalyon) — cizgili
export function GolOutline({ className = '', stroke = 'currentColor', style }) {
  return (
    <svg viewBox="0 0 100 100" className={className} style={style} aria-hidden="true" fill="none" stroke={stroke}>
      <path
        d="M50 6 L58 14 L58 22 L66 22 L78 34 L78 42 L86 42 L94 50 L86 58 L78 58 L78 66 L66 78 L58 78 L58 86 L50 94 L42 86 L42 78 L34 78 L22 66 L22 58 L14 58 L6 50 L14 42 L22 42 L22 34 L34 22 L42 22 L42 14 Z"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      <path d="M50 28 L72 50 L50 72 L28 50 Z" strokeWidth="2.2" />
      <path d="M50 42 L58 50 L50 58 L42 50 Z" strokeWidth="2.2" />
    </svg>
  )
}

// Böyük xalça gölü — nəticə ekranında xalın arxasında
export function Medallion({ className = '' }) {
  return (
    <svg viewBox="0 0 400 400" className={className} aria-hidden="true">
      <g stroke="var(--gold-500)" fill="none">
        <path
          d="M200 20 L228 48 L228 76 L256 76 L296 116 L296 144 L324 144 L380 200 L324 256 L296 256 L296 284 L256 324 L228 324 L228 352 L200 380 L172 352 L172 324 L144 324 L104 284 L104 256 L76 256 L20 200 L76 144 L104 144 L104 116 L144 76 L172 76 L172 48 Z"
          strokeWidth="3"
          strokeOpacity="0.75"
          fill="var(--red-50)"
        />
        <path d="M200 70 L330 200 L200 330 L70 200 Z" strokeWidth="2" strokeOpacity="0.5" fill="var(--teal-50)" />
        <path d="M200 108 L292 200 L200 292 L108 200 Z" strokeWidth="1.6" strokeOpacity="0.45" fill="none" />
        <path d="M200 146 L254 200 L200 254 L146 200 Z" strokeWidth="1.6" strokeOpacity="0.5" fill="var(--gold-50)" />
      </g>
      {[0, 45, 90, 135, 180, 225, 270, 315].map((a) => (
        <g key={a} transform={`rotate(${a} 200 200)`}>
          <path d={BUTA_D} transform="translate(178 2) scale(0.32)" fill="var(--gold-400)" fillOpacity="0.3" />
        </g>
      ))}
    </svg>
  )
}

// Açıq (ağ) fon: yumşaq keçid + çox seyrək naxış toxuması
export function SoftBackground() {
  return (
    <svg className="soft-bg" aria-hidden="true">
      <defs>
        <linearGradient id="paperGrad" x1="0" y1="0" x2="0.4" y2="1">
          <stop offset="0%" stopColor="#fffdf9" />
          <stop offset="55%" stopColor="#fbf6ec" />
          <stop offset="100%" stopColor="#f4ecdd" />
        </linearGradient>

        {/* seyrək plitə: bir göl, bir neçə xırda romb */}
        <pattern id="sparse" width="230" height="230" patternUnits="userSpaceOnUse">
          <g stroke="var(--red-700)" strokeOpacity="0.12" fill="none" strokeWidth="2">
            <path d="M115 74 L156 115 L115 156 L74 115 Z" />
            <path d="M115 96 L134 115 L115 134 L96 115 Z" strokeOpacity="0.16" />
          </g>
          <g fill="var(--teal-500)" fillOpacity="0.13">
            <path d="M10 10 L18 18 L10 26 L2 18 Z" />
            <path d="M225 10 L233 18 L225 26 L217 18 Z" />
            <path d="M10 225 L18 233 L10 241 L2 233 Z" />
            <path d="M225 225 L233 233 L225 241 L217 233 Z" />
          </g>
          <g fill="var(--gold-500)" fillOpacity="0.16">
            <path d="M115 8 L122 15 L115 22 L108 15 Z" />
            <path d="M8 115 L15 122 L8 129 L1 122 Z" />
            <path d="M222 115 L229 122 L222 129 L215 122 Z" />
            <path d="M115 222 L122 229 L115 236 L108 229 Z" />
          </g>
        </pattern>
      </defs>
      <rect width="100%" height="100%" fill="url(#paperGrad)" />
      <rect width="100%" height="100%" fill="url(#sparse)" />
    </svg>
  )
}

// Ekrana səpələnmiş milli naxışlar — hərəsi başqa ölçüdə, yavaş-yavaş süzülür
const SCATTER = [
  { kind: 'buta', left: '4%', top: '12%', size: 7, rot: -14, tone: 'red', o: 0.2, d: 0 },
  { kind: 'gol', left: '15%', top: '72%', size: 6, rot: 0, tone: 'teal', o: 0.18, d: 1.4 },
  { kind: 'star', left: '31%', top: '8%', size: 4.6, rot: 12, tone: 'gold', o: 0.26, d: 2.2 },
  { kind: 'buta', left: '46%', top: '84%', size: 5.5, rot: 8, tone: 'gold', o: 0.2, d: 0.7 },
  { kind: 'gol', left: '62%', top: '15%', size: 5, rot: 0, tone: 'red', o: 0.16, d: 3.1 },
  { kind: 'buta', left: '78%', top: '62%', size: 8, rot: 16, tone: 'teal', o: 0.16, d: 1.9 },
  { kind: 'star', left: '88%', top: '20%', size: 5.6, rot: -8, tone: 'red', o: 0.18, d: 2.6 },
  { kind: 'buta', left: '93%', top: '84%', size: 6, rot: -20, tone: 'gold', o: 0.2, d: 0.4 },
  { kind: 'gol', left: '37%', top: '44%', size: 4.2, rot: 0, tone: 'gold', o: 0.14, d: 3.6 },
  { kind: 'star', left: '8%', top: '44%', size: 3.6, rot: 0, tone: 'teal', o: 0.2, d: 1.1 },
  { kind: 'buta', left: '69%', top: '36%', size: 4.4, rot: -10, tone: 'red', o: 0.14, d: 2.9 },
  { kind: 'gol', left: '55%', top: '60%', size: 3.4, rot: 0, tone: 'teal', o: 0.16, d: 0.9 },
  { kind: 'star', left: '24%', top: '30%', size: 3.2, rot: 18, tone: 'gold', o: 0.18, d: 3.3 },
  { kind: 'buta', left: '85%', top: '46%', size: 3.6, rot: 22, tone: 'gold', o: 0.16, d: 1.6 },
]

export function ScatterMotifs() {
  return (
    <div className="scatter" aria-hidden="true">
      {SCATTER.map((m, i) => {
        const style = {
          left: m.left,
          top: m.top,
          width: `${m.size}rem`,
          opacity: m.o,
          '--r': `${m.rot}deg`,
          animationDelay: `${m.d}s`,
          animationDuration: `${9 + (i % 5) * 2}s`,
        }
        const cls = `motif motif-${m.tone}`
        if (m.kind === 'buta') return <ButaOutline key={i} className={cls} style={style} />
        if (m.kind === 'star') return <OctagramOutline key={i} className={cls} style={style} />
        return <GolOutline key={i} className={cls} style={style} />
      })}
    </div>
  )
}

// İncə xalça haşiyəsi: 4 zolaq + 4 künc ulduzu (açıq fon üçün yüngül variant)
export function CarpetFrame() {
  return (
    <div className="carpet-frame" aria-hidden="true">
      <svg className="frame-defs">
        <defs>
          <pattern id="hashiye-h" width="46" height="26" patternUnits="userSpaceOnUse">
            <rect width="46" height="26" fill="var(--ivory)" />
            <rect y="1" width="46" height="1.6" fill="var(--gold-500)" />
            <rect y="23.4" width="46" height="1.6" fill="var(--gold-500)" />
            <path d="M23 5 L32 13 L23 21 L14 13 Z" fill="none" stroke="var(--red-700)" strokeWidth="1.6" />
            <path d="M23 9.5 L26.5 13 L23 16.5 L19.5 13 Z" fill="var(--teal-500)" />
            <path d="M0 8 L5 13 L0 18 Z" fill="var(--gold-500)" />
            <path d="M46 8 L41 13 L46 18 Z" fill="var(--gold-500)" />
          </pattern>
          <pattern id="hashiye-v" width="26" height="46" patternUnits="userSpaceOnUse">
            <rect width="26" height="46" fill="var(--ivory)" />
            <rect x="1" width="1.6" height="46" fill="var(--gold-500)" />
            <rect x="23.4" width="1.6" height="46" fill="var(--gold-500)" />
            <path d="M13 14 L21 23 L13 32 L5 23 Z" fill="none" stroke="var(--red-700)" strokeWidth="1.6" />
            <path d="M13 19.5 L16.5 23 L13 26.5 L9.5 23 Z" fill="var(--teal-500)" />
            <path d="M8 0 L13 5 L18 0 Z" fill="var(--gold-500)" />
            <path d="M8 46 L13 41 L18 46 Z" fill="var(--gold-500)" />
          </pattern>
        </defs>
      </svg>

      <svg className="frame-strip strip-top" preserveAspectRatio="none">
        <rect width="100%" height="100%" fill="url(#hashiye-h)" />
      </svg>
      <svg className="frame-strip strip-bottom" preserveAspectRatio="none">
        <rect width="100%" height="100%" fill="url(#hashiye-h)" />
      </svg>
      <svg className="frame-strip strip-left" preserveAspectRatio="none">
        <rect width="100%" height="100%" fill="url(#hashiye-v)" />
      </svg>
      <svg className="frame-strip strip-right" preserveAspectRatio="none">
        <rect width="100%" height="100%" fill="url(#hashiye-v)" />
      </svg>

      {['tl', 'tr', 'bl', 'br'].map((c) => (
        <div key={c} className={`frame-corner corner-${c}`}>
          <Octagram fill="var(--ivory)" stroke="var(--gold-500)" inner={false} />
          <GolOutline className="frame-corner-gol" stroke="var(--red-700)" />
        </div>
      ))}
    </div>
  )
}

// Başlıq altında bəzək xətti: buta – xətt – ulduz – xətt – buta
export function Divider({ className = '' }) {
  return (
    <div className={`divider ${className}`} aria-hidden="true">
      <Buta className="divider-buta" flip />
      <span className="divider-line" />
      <Octagram className="divider-star" fill="var(--ivory)" stroke="var(--gold-500)" inner={false} />
      <span className="divider-line" />
      <Buta className="divider-buta" />
    </div>
  )
}
