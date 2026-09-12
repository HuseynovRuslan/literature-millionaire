import { Buta, Divider, Octagram } from './Ornaments'

export default function StartScreen({ quiz, questionCount, seconds, onStart, onBack }) {
  return (
    <section className="screen start-screen">
      <button className="btn-back" onClick={onBack}>
        ‹ KİTABLAR
      </button>

      <div className="crest">
        <Octagram className="crest-star" />
        <Buta className="crest-buta crest-buta-l" flip />
        <Buta className="crest-buta crest-buta-r" />
      </div>

      <div className="eyebrow">Ədəbiyyat Quizi</div>
      <h1 className="start-title">{quiz.title}</h1>
      <div className="start-author">{quiz.author}</div>

      <Divider />

      <div className="rules">
        <div className="rule">
          <Octagram className="rule-star" />
          <span className="rule-num">{questionCount}</span>
          <span className="rule-label">sual</span>
        </div>
        <div className="rule">
          <Octagram className="rule-star" />
          <span className="rule-num">{seconds}</span>
          <span className="rule-label">saniyə</span>
        </div>
        <div className="rule">
          <Octagram className="rule-star" />
          <span className="rule-num">250</span>
          <span className="rule-label">maks. xal</span>
        </div>
      </div>

      <p className="start-hint">Tez cavab ver — nə qədər tez, o qədər çox xal!</p>

      <button className="btn btn-primary btn-start" onClick={onStart}>
        <span>BAŞLA</span>
      </button>

      <div className="start-foot">Başlamaq üçün ekrana toxunun</div>
    </section>
  )
}
