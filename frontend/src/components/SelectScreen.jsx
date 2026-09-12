import Cover from './Cover'
import { Buta, Divider } from './Ornaments'

export default function SelectScreen({ quizzes, onPick }) {
  return (
    <section className="screen select-screen">
      <div className="eyebrow">Ədəbiyyat Quizi</div>
      <h1 className="select-title">Bir kitab seç</h1>
      <Divider />

      <div className="book-grid">
        {quizzes.map((q, i) => (
          <button
            key={q.id}
            className="book-card"
            style={{ animationDelay: `${120 + i * 110}ms` }}
            onClick={() => onPick(q)}
          >
            <div className="book-card-frame">
              <Cover quiz={q} className="book-card-cover" />
              <Buta className="bc-corner bc-tl" />
              <Buta className="bc-corner bc-br" flip />
            </div>
            <div className="book-card-title">{q.title}</div>
            <div className="book-card-author">{q.author}</div>
            {q.tag && <div className="book-card-tag">{q.tag}</div>}
            <span className="book-card-cta">OYNA</span>
          </button>
        ))}
      </div>

      <div className="start-foot">Oynamaq üçün kitaba toxunun</div>
    </section>
  )
}
