import { useCallback, useState } from 'react'
import { QUIZZES } from './data/quizzes'
import { buildRound } from './lib/random'
import { sound } from './lib/sound'
import { CarpetFrame, ScatterMotifs, SoftBackground } from './components/Ornaments'
import BookPanel from './components/BookPanel'
import FormScreen from './components/FormScreen'
import SelectScreen from './components/SelectScreen'
import StartScreen from './components/StartScreen'
import GameScreen from './components/GameScreen'
import ResultScreen from './components/ResultScreen'

const QUESTION_COUNT = 10
const SECONDS = 15

function goFullscreen() {
  const el = document.documentElement
  if (!document.fullscreenElement && el.requestFullscreen) el.requestFullscreen().catch(() => {})
}

export default function App() {
  const [screen, setScreen] = useState('form') // form | select | start | play | result
  const [player, setPlayer] = useState(null)
  const [quiz, setQuiz] = useState(null)
  const [round, setRound] = useState([])
  const [results, setResults] = useState([])
  const [roundId, setRoundId] = useState(0)
  const [muted, setMuted] = useState(false)

  const register = (data) => {
    sound.tap()
    goFullscreen()
    setPlayer(data)
    setScreen('select')
  }

  const pick = (q) => {
    sound.tap()
    setQuiz(q)
    setScreen('start')
  }

  const start = () => {
    sound.tap()
    setRound(buildRound(quiz.questions, QUESTION_COUNT))
    setRoundId((n) => n + 1)
    setScreen('play')
  }

  const finish = (res) => {
    setResults(res)
    setScreen('result')
    sound.finish()
  }

  const toLibrary = useCallback(() => setScreen('select'), [])
  const toIntro = useCallback(() => setScreen('start'), [])
  const exitUser = useCallback(() => {
    setPlayer(null)
    setQuiz(null)
    setScreen('form')
  }, [])

  const toggleMute = () => {
    sound.setMuted(!muted)
    setMuted(!muted)
  }

  const showBook = quiz && (screen === 'start' || screen === 'play' || screen === 'result')

  return (
    <div className="app">
      <SoftBackground />
      <ScatterMotifs />
      <CarpetFrame />

      <div className="controls">
        {screen === 'play' && (
          <button className="icon-btn" onClick={toIntro} aria-label="Kitab səhifəsi">
            <svg viewBox="0 0 24 24">
              <path d="M3 11 12 3l9 8M5 9.5V21h5v-6h4v6h5V9.5" />
            </svg>
          </button>
        )}
        <button className="icon-btn" onClick={toggleMute} aria-label={muted ? 'Səsi aç' : 'Səsi bağla'}>
          <svg viewBox="0 0 24 24">
            <path d="M4 9v6h4l5 4V5L8 9H4Z" />
            {muted ? <path d="m16 9 5 6m0-6-5 6" /> : <path d="M16 8.5a5 5 0 0 1 0 7M18.5 6a8.5 8.5 0 0 1 0 12" />}
          </svg>
        </button>
      </div>

      <div className={`brand ${screen === 'select' || screen === 'form' ? 'brand-lg' : ''}`}>
        <img src="/brand/bakiabadliq.jpg" alt="Bakı Abadlıq" draggable="false" />
      </div>

      {player && screen !== 'form' && (
        <div className="player-chip">
          <span>{player.ad} {player.soyad}</span>
          <button onClick={exitUser} aria-label="Çıxış">
            ✕
          </button>
        </div>
      )}

      <main className="stage">
        {showBook && <BookPanel quiz={quiz} />}
        <div className="content">
          {screen === 'form' && <FormScreen onDone={register} />}
          {screen === 'select' && <SelectScreen quizzes={QUIZZES} onPick={pick} />}
          {screen === 'start' && (
            <StartScreen
              quiz={quiz}
              questionCount={QUESTION_COUNT}
              seconds={SECONDS}
              onStart={start}
              onBack={toLibrary}
            />
          )}
          {screen === 'play' && <GameScreen key={roundId} questions={round} seconds={SECONDS} onFinish={finish} />}
          {screen === 'result' && (
            <ResultScreen
              results={results}
              player={player}
              quiz={quiz}
              onRestart={start}
              onLibrary={toLibrary}
              onExit={exitUser}
            />
          )}
        </div>
      </main>
    </div>
  )
}
