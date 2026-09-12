import { useState } from 'react'
import { Buta, GolOutline } from './Ornaments'

// Kitab üzlüyü. Şəkil faylı yoxdursa (hələ yüklənməyibsə),
// avtomatik olaraq naxışlı müvəqqəti üzlük göstərilir.
export default function Cover({ quiz, className = '' }) {
  const [broken, setBroken] = useState(false)

  if (!quiz.cover || broken) {
    return (
      <div className={`cover-fallback ${className}`}>
        <GolOutline className="cf-gol" stroke="var(--gold-500)" />
        <Buta className="cf-buta cf-buta-l" flip />
        <Buta className="cf-buta cf-buta-r" />
        <div className="cf-title">{quiz.title}</div>
        <div className="cf-line" />
        <div className="cf-author">{quiz.author}</div>
        <div className="cf-note">üz qabığı əlavə olunacaq</div>
      </div>
    )
  }

  return (
    <img
      src={quiz.cover}
      alt={`${quiz.author} — ${quiz.title}`}
      className={className}
      draggable="false"
      onError={() => setBroken(true)}
    />
  )
}
