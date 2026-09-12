import { useState } from 'react'
import Keyboard from './Keyboard'
import { Buta, Divider, Octagram } from './Ornaments'

const FIELDS = [
  { key: 'ad', label: 'Ad', mode: 'text', max: 24, hint: 'ən azı 2 hərf' },
  { key: 'soyad', label: 'Soyad', mode: 'text', max: 28, hint: 'ən azı 2 hərf' },
  { key: 'ataAdi', label: 'Ata adı', mode: 'text', max: 28, hint: 'ən azı 2 hərf' },
  { key: 'fin', label: 'FİN kod', mode: 'text', max: 7, hint: '7 simvol' },
  { key: 'telefon', label: 'Telefon', mode: 'digits', max: 9, prefix: '+994', hint: '9 rəqəm' },
]

const EMPTY = { ad: '', soyad: '', ataAdi: '', fin: '', telefon: '' }

export function formatPhone(d) {
  if (!d) return ''
  const p = [d.slice(0, 2), d.slice(2, 5), d.slice(5, 7), d.slice(7, 9)].filter(Boolean)
  return p.join(' ')
}

function isValid(key, value) {
  if (key === 'fin') return value.length === 7
  if (key === 'telefon') return value.length === 9
  return value.trim().length >= 2
}

export default function FormScreen({ onDone }) {
  const [values, setValues] = useState(EMPTY)
  const [active, setActive] = useState(0)
  const [touched, setTouched] = useState(false)

  const field = FIELDS[active]
  const allValid = FIELDS.every((f) => isValid(f.key, values[f.key]))

  const type = (ch) => {
    if (field.mode === 'digits' && !/[0-9]/.test(ch)) return
    setValues((v) => {
      const cur = v[field.key]
      if (cur.length >= field.max) return v
      return { ...v, [field.key]: cur + ch }
    })
  }

  const back = () => setValues((v) => ({ ...v, [field.key]: v[field.key].slice(0, -1) }))

  const next = () => {
    if (active < FIELDS.length - 1) {
      setActive(active + 1)
      return
    }
    submit()
  }

  const submit = () => {
    setTouched(true)
    if (!allValid) {
      const bad = FIELDS.findIndex((f) => !isValid(f.key, values[f.key]))
      if (bad >= 0) setActive(bad)
      return
    }
    onDone({
      ...values,
      ad: values.ad.trim(),
      soyad: values.soyad.trim(),
      ataAdi: values.ataAdi.trim(),
      telefonTam: `+994${values.telefon}`,
    })
  }

  return (
    <section className="screen form-screen">
      <div className="form-left">
        <div className="crest form-crest">
          <Octagram className="crest-star" />
          <Buta className="crest-buta crest-buta-l" flip />
          <Buta className="crest-buta crest-buta-r" />
        </div>

        <div className="eyebrow">Ədəbiyyat Quizi</div>
        <h1 className="form-title">Qeydiyyat</h1>
        <p className="form-sub">Oyuna başlamaq üçün məlumatlarınızı yazın</p>
        <Divider />

        <div className="fields">
          {FIELDS.map((f, i) => {
            const val = values[f.key]
            const ok = isValid(f.key, val)
            const shown = f.key === 'telefon' ? formatPhone(val) : val
            return (
              <button
                key={f.key}
                className={`field ${i === active ? 'is-active' : ''} ${touched && !ok ? 'is-bad' : ''}`}
                onClick={() => setActive(i)}
              >
                <span className="field-label">{f.label}</span>
                <span className="field-value">
                  {f.prefix && <i className="field-prefix">{f.prefix}</i>}
                  <span>{shown}</span>
                  {i === active && <i className="caret" />}
                  {!shown && i !== active && <i className="field-hint">{f.hint}</i>}
                </span>
              </button>
            )
          })}
        </div>

        <button className={`btn btn-primary form-submit ${allValid ? '' : 'is-off'}`} onClick={submit}>
          <span>DAVAM ET</span>
        </button>

        <div className="form-note">Məlumatlar yalnız bu cihazda saxlanılır</div>
      </div>

      <div className="form-right">
        <Keyboard
          mode={field.mode}
          onKey={type}
          onBackspace={back}
          onNext={next}
          nextLabel={active === FIELDS.length - 1 ? 'HAZIRDIR' : 'NÖVBƏTİ'}
        />
      </div>
    </section>
  )
}
