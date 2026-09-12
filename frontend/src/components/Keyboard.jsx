import { sound } from '../lib/sound'

// Sensor ekran üçün ekran klaviaturası (Azərbaycan əlifbası, əlifba sırası ilə)
const DIGITS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0']
const LETTERS = [
  ['A', 'B', 'C', 'Ç', 'D', 'E', 'Ə', 'F', 'G', 'Ğ'],
  ['H', 'X', 'I', 'İ', 'J', 'K', 'Q', 'L', 'M', 'N'],
  ['O', 'Ö', 'P', 'R', 'S', 'Ş', 'T', 'U', 'Ü', 'V'],
  ['Y', 'Z'],
]

export default function Keyboard({ mode = 'text', onKey, onBackspace, onNext, nextLabel = 'NÖVBƏTİ' }) {
  const press = (fn) => () => {
    sound.tap()
    fn()
  }

  return (
    <div className="keyboard">
      <div className="kb-row">
        {DIGITS.map((d) => (
          <button key={d} className="kb-key" onClick={press(() => onKey(d))}>
            {d}
          </button>
        ))}
      </div>

      {mode !== 'digits' &&
        LETTERS.map((row, i) => (
          <div className="kb-row" key={i}>
            {row.map((ch) => (
              <button key={ch} className="kb-key" onClick={press(() => onKey(ch))}>
                {ch}
              </button>
            ))}
            {i === LETTERS.length - 1 && (
              <button className="kb-key kb-space" onClick={press(() => onKey(' '))}>
                BOŞLUQ
              </button>
            )}
          </div>
        ))}

      <div className="kb-row kb-row-actions">
        {mode === 'digits' && (
          <button className="kb-key kb-space" onClick={press(() => onKey(' '))} disabled>
            &nbsp;
          </button>
        )}
        <button className="kb-key kb-back" onClick={press(onBackspace)}>
          ← SİL
        </button>
        <button className="kb-key kb-next" onClick={press(onNext)}>
          {nextLabel}
        </button>
      </div>
    </div>
  )
}
