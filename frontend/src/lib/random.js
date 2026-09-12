export function shuffle(arr) {
  const a = [...arr]
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[a[i], a[j]] = [a[j], a[i]]
  }
  return a
}

const rand = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min

// Bankdan `count` sual seçir, hər sualın variantlarını qarışdırır
export function buildRound(questions, count) {
  return shuffle(questions)
    .slice(0, count)
    .map((item) => {
      const opts = shuffle(item.options.map((text, i) => ({ text, correct: i === item.answer })))
      return {
        q: item.q,
        options: opts.map((o) => o.text),
        answer: opts.findIndex((o) => o.correct),
      }
    })
}

const FIRST = [
  'Aysel', 'Murad', 'Nigar', 'Elvin', 'Leyla', 'Tural', 'Günay', 'Rəşad', 'Səbinə', 'Orxan',
  'Nərmin', 'Kamran', 'Fidan', 'Elçin', 'Lalə', 'Vüsal', 'Aytən', 'Ramil', 'Zəhra', 'Emil',
  'Türkan', 'Fərid', 'Sevinc', 'Nihad', 'Könül', 'Samir', 'Aynur', 'Ülvi', 'Nurlan', 'Şəbnəm',
]
const INITIALS = 'ABCDEFGHİKLMNOQRSTVYZ'.split('')

// Oyunçunun nəticəsini təsadüfi adlarla birlikdə lider tablosuna yerləşdirir
// (maksimum xal 10 olduğuna görə təsadüfi xallar da 2–10 aralığındadır)
export function buildLeaderboard(playerScore, playerName = 'SƏN', size = 10) {
  const names = shuffle(FIRST).slice(0, size - 1)
  const rows = names.map((n) => ({
    name: `${n} ${INITIALS[rand(0, INITIALS.length - 1)]}.`,
    score: rand(2, 10),
    me: false,
  }))
  rows.push({ name: playerName, score: playerScore, me: true })
  // bərabər xalda oyunçu yuxarıda dursun
  return rows.sort((a, b) => b.score - a.score || (b.me ? 1 : 0) - (a.me ? 1 : 0))
}
