// İştirakçıların nəticələri yalnız bu kompüterin brauzerində saxlanılır
// (heç yerə göndərilmir). Son 500 qeyd saxlanılır.
const KEY = 'quiz-oyun:istirakcilar'
const LIMIT = 500

export function saveResult(record) {
  try {
    const list = JSON.parse(localStorage.getItem(KEY) || '[]')
    list.push({ ...record, tarix: new Date().toISOString() })
    localStorage.setItem(KEY, JSON.stringify(list.slice(-LIMIT)))
  } catch {
    /* yaddaş əlçatmazdırsa, oyun yenə də davam etsin */
  }
}

export function loadResults() {
  try {
    return JSON.parse(localStorage.getItem(KEY) || '[]')
  } catch {
    return []
  }
}
