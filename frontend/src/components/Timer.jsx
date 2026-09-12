import { Octagram } from './Ornaments'

const R = 46
const C = 2 * Math.PI * R

export default function Timer({ remainingMs, totalMs }) {
  const frac = Math.max(0, remainingMs / totalMs)
  const secs = Math.ceil(remainingMs / 1000)
  const danger = secs <= 5
  return (
    <div className={`timer ${danger ? 'timer-danger' : ''}`}>
      <Octagram className="timer-star" />
      <svg viewBox="0 0 100 100" className="timer-ring" aria-hidden="true">
        <circle cx="50" cy="50" r={R} className="timer-track" />
        <circle
          cx="50"
          cy="50"
          r={R}
          className="timer-progress"
          strokeDasharray={C}
          strokeDashoffset={C * (1 - frac)}
        />
      </svg>
      <span className="timer-num">{secs}</span>
    </div>
  )
}
