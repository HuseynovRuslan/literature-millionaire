import Cover from './Cover'
import { Buta } from './Ornaments'

export default function BookPanel({ quiz }) {
  return (
    <aside className="book-panel">
      <div className="book-stage">
        <div className="book-glow" />
        <div className="book-frame">
          <div className="book-frame-inner">
            <Cover quiz={quiz} className="book-cover" />
          </div>
          <Buta className="corner corner-tl" />
          <Buta className="corner corner-tr" flip />
          <Buta className="corner corner-bl" />
          <Buta className="corner corner-br" flip />
        </div>
      </div>

      <div className="plaque">
        <Buta className="plaque-buta" flip />
        <div className="plaque-text">
          <div className="book-title">{quiz.title}</div>
          <div className="book-author">{quiz.author}</div>
        </div>
        <Buta className="plaque-buta" />
      </div>
    </aside>
  )
}
