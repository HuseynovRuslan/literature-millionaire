import { useEffect, useState, type FormEvent } from 'react'
import { getBooks } from '../api/books'
import { isAxiosError } from 'axios'
import { createQuestion, getQuestions } from '../api/questions'
import type { BookListItem } from '../types/book'
import type { CorrectOption, Difficulty, Question } from '../types/question'

const DIFFICULTIES: Array<{ value: Difficulty | ''; label: string }> = [
  { value: '', label: 'Hamısı' },
  { value: 'Easy', label: 'Asan' },
  { value: 'Medium', label: 'Orta' },
  { value: 'Hard', label: 'Çətin' },
]

const difficultyBadge: Record<Difficulty, string> = {
  Easy: 'bg-emerald-500/15 text-emerald-300',
  Medium: 'bg-amber-500/15 text-amber-300',
  Hard: 'bg-rose-500/15 text-rose-300',
}

const emptyForm = {
  text: '',
  optionA: '',
  optionB: '',
  optionC: '',
  optionD: '',
  correctOption: 'A' as CorrectOption,
  difficulty: 'Easy' as Difficulty,
  category: '',
  explanation: '',
  bookId: '',
  imageUrl: '',
  imageAltText: '',
}

/** Mirrors the backend rule: a local asset under /question-images/ with an image extension, nothing else. */
const IMAGE_PATH = /^\/question-images\/[A-Za-z0-9._-]+\.(webp|png|jpg|jpeg)$/i
function imageFieldsError(imageUrl: string, imageAltText: string): string | null {
  const url = imageUrl.trim()
  const alt = imageAltText.trim()
  if (!url && !alt) return null
  if (url && !alt) return 'Şəkil yolu yazılıbsa, şəkil təsviri də yazılmalıdır.'
  if (!url && alt) return 'Şəkil təsviri yazılıbsa, şəkil yolu da yazılmalıdır.'
  if (!IMAGE_PATH.test(url) || url.includes('..')) return 'Şəkil yolu /question-images/ad.webp formatında olmalıdır (webp, png, jpg və ya jpeg; xarici URL, qovluq və ya sorğu parametrləri olmadan).'
  return null
}

/** Turns a 400 ValidationProblemDetails from the API into one Azerbaijani line. */
function describeApiError(err: unknown): string {
  if (isAxiosError(err) && err.response?.status === 400) {
    const errors = (err.response.data as { errors?: Record<string, string[]> } | undefined)?.errors ?? {}
    const keys = Object.keys(errors)
    if (keys.some((k) => /^imageurl$/i.test(k))) return 'Şəkil yolu düzgün deyil: /question-images/ad.webp formatı gözlənilir.'
    if (keys.some((k) => /^imagealttext$/i.test(k))) return 'Şəkil yolu və şəkil təsviri birlikdə doldurulmalıdır.'
    if (keys.some((k) => /^bookid$/i.test(k))) return 'Seçilmiş kitab mövcud deyil.'
    if (keys.length) return `Yoxlama xətası: ${keys.join(', ')}`
  }
  return err instanceof Error ? err.message : 'Sual yaradılarkən xəta baş verdi.'
}

export default function AdminQuestionsPage() {
  const [difficulty, setDifficulty] = useState<Difficulty | ''>('')
  const [bookId, setBookId] = useState('')
  const [books, setBooks] = useState<BookListItem[]>([])
  const [booksLoading, setBooksLoading] = useState(true)
  const [booksError, setBooksError] = useState<string | null>(null)
  const [reloadVersion, setReloadVersion] = useState(0)
  const [result, setResult] = useState<{
    filterKey: string
    questions: Question[]
    error: string | null
  } | null>(null)
  const [form, setForm] = useState(emptyForm)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [formSuccess, setFormSuccess] = useState<string | null>(null)

  const filterKey = `${difficulty}:${bookId}:${reloadVersion}`
  const loading = result?.filterKey !== filterKey
  const questions = result?.questions ?? []
  const error = loading ? null : result?.error ?? null

  useEffect(() => {
    let cancelled = false

    getBooks()
      .then((data) => {
        if (!cancelled) setBooks(data)
      })
      .catch((err: unknown) => {
        if (!cancelled) setBooksError(err instanceof Error ? err.message : 'Naməlum xəta')
      })
      .finally(() => {
        if (!cancelled) setBooksLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    let cancelled = false
    const filter = {
      ...(difficulty ? { difficulty } : {}),
      ...(bookId ? { bookId: Number(bookId) } : {}),
    }

    getQuestions(filter)
      .then((data) => {
        if (!cancelled) setResult({ filterKey, questions: data, error: null })
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setResult({
            filterKey,
            questions: [],
            error: err instanceof Error ? err.message : 'Naməlum xəta',
          })
        }
      })

    return () => {
      cancelled = true
    }
  }, [bookId, difficulty, filterKey, reloadVersion])

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const selectedBookId = Number(form.bookId)

    if (!Number.isInteger(selectedBookId) || selectedBookId <= 0) {
      setFormError('Kitab seçilməlidir.')
      setFormSuccess(null)
      return
    }

    const mediaError = imageFieldsError(form.imageUrl, form.imageAltText)
    if (mediaError) {
      setFormError(mediaError)
      setFormSuccess(null)
      return
    }

    setSaving(true)
    setFormError(null)
    setFormSuccess(null)

    try {
      await createQuestion({
        text: form.text,
        optionA: form.optionA,
        optionB: form.optionB,
        optionC: form.optionC,
        optionD: form.optionD,
        correctOption: form.correctOption,
        difficulty: form.difficulty,
        category: form.category,
        explanation: form.explanation || null,
        bookId: selectedBookId,
        imageUrl: form.imageUrl.trim() || null,
        imageAltText: form.imageAltText.trim() || null,
      })
      setForm({ ...emptyForm, bookId: form.bookId })
      setFormSuccess('Sual yaradıldı.')
      setReloadVersion((value) => value + 1)
    } catch (err: unknown) {
      setFormError(describeApiError(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="space-y-6">
      <header>
        <h1 className="text-3xl font-bold text-white">Suallar</h1>
        <p className="text-slate-400">Sual bazasının idarə edilməsi</p>
      </header>

      <form onSubmit={handleCreate} className="space-y-4 rounded-lg border border-slate-800 bg-slate-900/40 p-5">
        <h2 className="text-lg font-semibold text-white">Yeni sual</h2>

        <div className="grid gap-4 md:grid-cols-3">
          <label className="space-y-1 text-sm text-slate-300">
            <span>Kitab</span>
            <select
              required
              value={form.bookId}
              disabled={booksLoading || books.length === 0}
              onInvalid={() => setFormError('Kitab seçilməlidir.')}
              onChange={(e) => {
                setForm({ ...form, bookId: e.target.value })
                setFormError(null)
              }}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none disabled:opacity-60"
            >
              <option value="">{booksLoading ? 'Kitablar yüklənir…' : 'Kitab seçin'}</option>
              {books.map((book) => (
                <option key={book.id} value={book.id}>
                  {book.title} — {book.author}{book.isActive ? '' : ' (aktiv deyil)'}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-1 text-sm text-slate-300">
            <span>Kateqoriya</span>
            <input
              required
              maxLength={100}
              value={form.category}
              onChange={(e) => setForm({ ...form, category: e.target.value })}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none"
            />
          </label>

          <label className="space-y-1 text-sm text-slate-300">
            <span>Çətinlik</span>
            <select
              value={form.difficulty}
              onChange={(e) => setForm({ ...form, difficulty: e.target.value as Difficulty })}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none"
            >
              {DIFFICULTIES.filter((item) => item.value).map((item) => (
                <option key={item.value} value={item.value}>{item.label}</option>
              ))}
            </select>
          </label>
        </div>

        <label className="block space-y-1 text-sm text-slate-300">
          <span>Sual</span>
          <textarea
            required
            maxLength={1000}
            rows={3}
            value={form.text}
            onChange={(e) => setForm({ ...form, text: e.target.value })}
            className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none"
          />
        </label>

        <div className="grid gap-3 md:grid-cols-2">
          {(['A', 'B', 'C', 'D'] as const).map((option) => {
            const field = `option${option}` as const
            return (
              <label key={option} className="flex items-center gap-2 text-sm text-slate-300">
                <span className="w-5 font-mono text-amber-300">{option}</span>
                <input
                  required
                  maxLength={300}
                  value={form[field]}
                  onChange={(e) => setForm({ ...form, [field]: e.target.value })}
                  className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none"
                />
              </label>
            )
          })}
        </div>

        <div className="grid gap-4 md:grid-cols-3">
          <label className="space-y-1 text-sm text-slate-300">
            <span>Düzgün cavab</span>
            <select
              value={form.correctOption}
              onChange={(e) => setForm({ ...form, correctOption: e.target.value as CorrectOption })}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none"
            >
              {(['A', 'B', 'C', 'D'] as const).map((option) => (
                <option key={option} value={option}>{option}</option>
              ))}
            </select>
          </label>

          <label className="space-y-1 text-sm text-slate-300 md:col-span-2">
            <span>İzah (istəyə bağlı)</span>
            <input
              maxLength={2000}
              value={form.explanation}
              onChange={(e) => setForm({ ...form, explanation: e.target.value })}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none"
            />
          </label>

          <label className="space-y-1 text-sm text-slate-300">
            <span>Şəkil yolu (istəyə bağlı)</span>
            <input
              maxLength={500}
              placeholder="/question-images/example.webp"
              value={form.imageUrl}
              onChange={(e) => setForm({ ...form, imageUrl: e.target.value })}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none"
            />
            <span className="block text-xs text-slate-500">Nümunə: /question-images/example.webp. Fayl frontend/public/question-images qovluğunda olmalıdır.</span>
          </label>

          <label className="space-y-1 text-sm text-slate-300">
            <span>Şəkil təsviri (istəyə bağlı)</span>
            <input
              maxLength={300}
              value={form.imageAltText}
              onChange={(e) => setForm({ ...form, imageAltText: e.target.value })}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-slate-100 focus:border-amber-400 focus:outline-none"
            />
            <span className="block text-xs text-slate-500">Hər iki sahə birlikdə doldurulur və ya birlikdə boş qalır. Təsvirdə düzgün cavabı yazmayın.</span>
          </label>
        </div>

        {booksError && <p className="text-sm text-rose-300">Kitablar yüklənmədi: {booksError}</p>}
        {formError && <p className="text-sm text-rose-300">{formError}</p>}
        {formSuccess && <p className="text-sm text-emerald-300">{formSuccess}</p>}

        <button
          type="submit"
          disabled={saving || booksLoading || books.length === 0}
          className="rounded-md bg-amber-400 px-4 py-2 font-semibold text-slate-950 hover:bg-amber-300 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {saving ? 'Yaradılır…' : 'Sual yarat'}
        </button>
      </form>

      <div className="flex flex-wrap items-end justify-end gap-4">
        <label className="flex items-center gap-2 text-sm text-slate-300">
          Kitab
          <select
            value={bookId}
            onChange={(e) => setBookId(e.target.value)}
            className="rounded-md border border-slate-700 bg-slate-900 px-3 py-1.5 text-slate-100 focus:border-amber-400 focus:outline-none"
          >
            <option value="">Hamısı</option>
            {books.map((book) => (
              <option key={book.id} value={book.id}>{book.title}</option>
            ))}
          </select>
        </label>

        <label className="flex items-center gap-2 text-sm text-slate-300">
          Çətinlik
          <select
            value={difficulty}
            onChange={(e) => setDifficulty(e.target.value as Difficulty | '')}
            className="rounded-md border border-slate-700 bg-slate-900 px-3 py-1.5 text-slate-100 focus:border-amber-400 focus:outline-none"
          >
            {DIFFICULTIES.map((item) => (
              <option key={item.value} value={item.value}>{item.label}</option>
            ))}
          </select>
        </label>
      </div>

      {loading && <p className="text-slate-400">Yüklənir…</p>}

      {error && (
        <div className="rounded-md border border-rose-500/40 bg-rose-500/10 p-4 text-sm text-rose-200">
          API ilə əlaqə qurulmadı: {error}. Backend işləyirmi?
        </div>
      )}

      {!loading && !error && questions.length === 0 && <p className="text-slate-400">Sual tapılmadı.</p>}

      {!loading && !error && questions.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-slate-800">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-900 text-xs uppercase tracking-wide text-slate-400">
              <tr>
                <th className="px-4 py-3">#</th>
                <th className="px-4 py-3">Sual</th>
                <th className="px-4 py-3">Kitab</th>
                <th className="px-4 py-3">Kateqoriya</th>
                <th className="px-4 py-3">Çətinlik</th>
                <th className="px-4 py-3">Düzgün</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {questions.map((question) => (
                <tr key={question.id} className="hover:bg-slate-900/60">
                  <td className="px-4 py-3 text-slate-500">{question.id}</td>
                  <td className="max-w-md px-4 py-3 text-slate-100">{question.text}</td>
                  <td className="px-4 py-3 text-slate-300">
                    {question.bookTitle ?? <span className="text-amber-300">Kitab təyin edilməyib</span>}
                  </td>
                  <td className="px-4 py-3 text-slate-300">{question.category}</td>
                  <td className="px-4 py-3">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${difficultyBadge[question.difficulty]}`}>
                      {question.difficulty}
                    </span>
                  </td>
                  <td className="px-4 py-3 font-mono text-amber-300">{question.correctOption}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
