import { useEffect, useRef, useState } from 'react'
import Timer from './Timer'
import { Buta } from './Ornaments'
import { sound } from '../lib/sound'

const LETTERS = ['A', 'B', 'C', 'D']
const REVEAL_MS = 1800

// Hər düzgün cavab 1 xal — 10 sualda maksimum 10 xal
const POINT = 1

export default function GameScreen({ questions, seconds, onFinish }) {
  const totalMs = seconds * 1000
  const [index, setIndex] = useState(0)
  const [selected, setSelected] = useState(null) // null — cavab gözlənilir, -1 — vaxt bitdi
  const [remaining, setRemaining] = useState(totalMs)
  const [results, setResults] = useState([])
  const [gain, setGain] = useState(null)

  const deadline = useRef(0)
  const locked = useRef(false)
  const timer = useRef(null)
  const nextTimeout = useRef(null)
  const resultsRef = useRef([])

  const q = questions[index]
  const revealed = selected !== null
  const score = results.reduce((s, r) => s + r.points, 0)

  function answer(choice) {
    if (locked.current) return
    locked.current = true
    clearInterval(timer.current)

    const correct = choice === q.answer
    const points = correct ? POINT : 0
    const next = [...resultsRef.current, { correct, points }]
    resultsRef.current = next

    setSelected(choice ?? -1)
    setResults(next)
    setGain(correct ? points : null)
    correct ? sound.correct() : sound.wrong()

    nextTimeout.current = setTimeout(() => {
      if (index + 1 < questions.length) {
        setIndex(index + 1)
      } else {
        onFinish(next)
      }
    }, REVEAL_MS)
  }

  // Hər yeni sual üçün taymeri sıfırla
  useEffect(() => {
    locked.current = false
    setSelected(null)
    setGain(null)
    setRemaining(totalMs)
    deadline.current = performance.now() + totalMs
    let lastSec = seconds

    timer.current = setInterval(() => {
      const rem = Math.max(0, deadline.current - performance.now())
      setRemaining(rem)
      const s = Math.ceil(rem / 1000)
      if (s !== lastSec) {
        lastSec = s
        if (s > 0 && s <= 5) sound.tick()
      }
      if (rem <= 0) answer(null)
    }, 100)

    return () => clearInterval(timer.current)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [index])

  useEffect(() => () => clearTimeout(nextTimeout.current), [])

  function optionClass(i) {
    if (!revealed) return ''
    if (i === q.answer) return 'is-correct'
    if (i === selected) return 'is-wrong'
    return 'is-dim'
  }

  return (
    <section className="screen game-screen">
      <header className="game-top">
        <div className="progress" aria-label={`Sual ${index + 1} / ${questions.length}`}>
          {questions.map((_, i) => {
            const r = results[i]
            const state = r ? (r.correct ? 'ok' : 'bad') : i === index ? 'current' : 'todo'
            return <Buta key={i} className={`progress-buta progress-${state}`} />
          })}
        </div>
        <div className="score-box">
          <span className="score-label">XAL</span>
          <span key={score} className="score-value bump">
            {score}
            <i className="score-max">/{questions.length}</i>
          </span>
        </div>
        <Timer remainingMs={remaining} totalMs={totalMs} />
      </header>

      <div className="top-rule" aria-hidden="true" />

      <div key={index} className="question-card enter">
        <Buta className="card-corner cc-tl" />
        <Buta className="card-corner cc-tr" flip />
        <Buta className="card-corner cc-bl" />
        <Buta className="card-corner cc-br" flip />

        <div className="question-ribbon">
          SUAL {index + 1} <span>/ {questions.length}</span>
        </div>
        <h2 className="question-text">{q.q}</h2>
        {gain !== null && <div className="gain">+{gain}</div>}
        {selected === -1 && <div className="timeout-badge">Vaxt bitdi!</div>}
      </div>

      <div key={`o${index}`} className="options">
        {q.options.map((text, i) => (
          <button
            key={i}
            className={`option ${optionClass(i)}`}
            style={{ animationDelay: `${120 + i * 70}ms` }}
            onClick={() => {
              sound.tap()
              answer(i)
            }}
            disabled={revealed}
          >
            <span className="option-letter">
              <span>{LETTERS[i]}</span>
            </span>
            <span className="option-text">{text}</span>
          </button>
        ))}
      </div>
    </section>
  )
}
