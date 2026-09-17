import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  adminFailure,
  bookErrors,
  bookInput,
  createBook,
  getBooks,
  updateBook,
  type AdminBook,
  type BookInput,
} from '../../api/admin'
import CoverPicker from '../../components/admin/CoverPicker'
import { useAdmin } from './adminContext'
import { formatDay } from './campaignDates'

const EMPTY: BookInput = { title: '', author: '', description: '', coverImageUrl: '', isActive: true }

type Editing = { kind: 'new' } | { kind: 'existing'; book: AdminBook }

/**
 * /admin/books: the question banks campaigns are built around. Only one kind of bank is a book — "Ayın Kitabı"
 * is played from the month's book, while "Yaşıl Bakı" or "Bilik yarışı" are collections with no author. Every
 * question belongs to one of them, so this is where a new category or a new month starts: add the bank, then
 * open its campaign.
 *
 * Nothing is deleted here — a bank that should no longer be offered is switched off, and what was played from
 * it stays readable.
 */
export default function AdminBooksPage() {
  const { onSignedOut } = useAdmin()
  const [books, setBooks] = useState<AdminBook[] | null>(null)
  const [failed, setFailed] = useState(false)
  const [editing, setEditing] = useState<Editing | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const fail = useCallback((err: unknown, otherwise: () => void) => {
    const failure = adminFailure(err)
    if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
    else otherwise()
  }, [onSignedOut])

  useEffect(() => {
    const controller = new AbortController()
    getBooks(controller.signal)
      .then((list) => { setBooks(list); setFailed(false) })
      .catch((err) => { if (!controller.signal.aborted) fail(err, () => setFailed(true)) })
    return () => controller.abort()
  }, [fail])

  function saved(book: AdminBook, wasNew: boolean) {
    setBooks((current) => {
      const rest = (current ?? []).filter((b) => b.id !== book.id)
      return [...rest, book].sort((a, b) => a.title.localeCompare(b.title, 'az'))
    })
    setEditing(null)
    setNotice(wasNew ? `"${book.title}" əlavə olundu.` : `"${book.title}" yadda saxlanıldı.`)
  }

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="books-title">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="books-title" className="font-display text-2xl font-bold">Sual bankları</h1>
          <p className="mt-1 max-w-[62ch] text-sm text-fg-2">
            Hər sual bir banka bağlıdır, kampaniya da bank üzərində qurulur. Bank həm kitab ola bilər
            ("Ayın Kitabı" — ad, müəllif, üz qabığı), həm də kolleksiya ("Yaşıl Bakı", "Bilik yarışı" — müəllif olmaya bilər).
            Yeni ay və ya yeni kateqoriya üçün əvvəlcə bankı əlavə edin, sonra Kampaniyalar bölməsindən ona kampaniya açın.
          </p>
        </div>
        {!editing && (
          <button type="button" onClick={() => { setNotice(null); setEditing({ kind: 'new' }) }} className="btn btn-primary min-h-11 px-5 text-sm">
            Yeni bank
          </button>
        )}
      </div>

      {notice && (
        <p role="status" data-testid="books-notice" className="mt-5 rounded-2xl bg-ok/10 px-4 py-3 text-sm font-semibold text-fg ring-1 ring-ok/30">
          {notice}
        </p>
      )}

      {editing && (
        <BookForm
          key={editing.kind === 'new' ? 'new' : editing.book.id}
          initial={editing.kind === 'new' ? EMPTY : bookInput(editing.book)}
          existing={editing.kind === 'new' ? null : editing.book}
          onCancel={() => setEditing(null)}
          onSaved={saved}
          onSignedOut={onSignedOut}
        />
      )}

      {failed && <p role="alert" className="mt-6 font-semibold text-fg">Sual banklarını yükləmək alınmadı.</p>}
      {!books && !failed && <p role="status" className="mt-6 text-fg-2">Yüklənir…</p>}
      {books?.length === 0 && !editing && <p className="mt-6 text-fg-2">Hələ sual bankı yoxdur.</p>}

      {books && books.length > 0 && (
        <ul className="mt-6 flex flex-col gap-3" data-testid="book-list">
          {books.map((book) => (
            <li key={book.id} data-testid={`book-${book.id}`}
              className={`flex gap-4 rounded-2xl px-4 py-4 ring-1 ${book.isActive ? 'bg-white/[0.04] ring-white/10' : 'bg-white/[0.02] ring-white/[0.06]'}`}>
              <div className="h-28 w-20 shrink-0 overflow-hidden rounded-xl bg-ink-950 ring-1 ring-white/10">
                {book.coverImageUrl
                  ? <img src={book.coverImageUrl} alt="" loading="lazy" className="h-full w-full object-cover" />
                  : <span className="grid h-full w-full place-items-center text-center text-[0.65rem] text-fg-3">Şəkil yoxdur</span>}
              </div>

              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2">
                  <span className="font-display text-lg font-bold text-fg">{book.title}</span>
                  {!book.isActive && (
                    <span className="rounded-full bg-white/[0.06] px-2.5 py-0.5 text-xs font-bold text-fg-3 ring-1 ring-white/15">Deaktiv</span>
                  )}
                </p>
                <p className="text-sm text-fg-2">{book.author}</p>
                {book.description && <p className="mt-1 line-clamp-2 text-sm text-fg-3">{book.description}</p>}
                <p className="mt-2 text-xs font-semibold text-fg-3">
                  {book.questionCount} sual · {book.campaignCount} kampaniya
                </p>
                {book.campaigns.length > 0 && (
                  <ul className="mt-1 flex flex-wrap gap-x-3 gap-y-1 text-xs text-fg-3">
                    {book.campaigns.slice(0, 3).map((campaign) => (
                      <li key={campaign.id}>
                        <Link to={`/admin/campaigns/${campaign.id}`} className="underline decoration-white/20 hover:text-fg">
                          #{campaign.id} {campaign.quizModeTitle} · {formatDay(campaign.startDate)} – {formatDay(campaign.endDate)}
                          {campaign.isEnabled ? '' : ' (deaktiv)'}
                        </Link>
                      </li>
                    ))}
                  </ul>
                )}
                <div className="mt-3">
                  <button type="button" onClick={() => { setNotice(null); setEditing({ kind: 'existing', book }) }}
                    className="btn btn-secondary min-h-10 px-4 text-sm">
                    Düzəlt
                  </button>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function BookForm({ initial, existing, onCancel, onSaved, onSignedOut }: {
  initial: BookInput
  existing: AdminBook | null
  onCancel: () => void
  onSaved: (book: AdminBook, wasNew: boolean) => void
  onSignedOut: () => void
}) {
  const [form, setForm] = useState<BookInput>(initial)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  function set<K extends keyof BookInput>(key: K, value: BookInput[K]) {
    setForm((current) => ({ ...current, [key]: value }))
    setErrors((current) => {
      if (!(key in current)) return current
      const next = { ...current }
      delete next[key]
      return next
    })
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setSaving(true)
    setFailure(null)
    try {
      const book = existing ? await updateBook(existing.id, form) : await createBook(form)
      onSaved(book, existing === null)
    } catch (err) {
      const fieldErrors = bookErrors(err)
      if (fieldErrors) {
        setErrors(fieldErrors)
        setFailure('Yadda saxlanmadı. Qırmızı ilə göstərilənləri düzəldin.')
      } else {
        const kind = adminFailure(err)
        if (kind === 'signed-out' || kind === 'not-an-admin') onSignedOut()
        else setFailure('Serverlə əlaqə alınmadı. Yenidən cəhd edin.')
      }
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={(e) => void submit(e)} noValidate data-testid="book-form"
      className="mt-6 flex max-w-[44rem] flex-col gap-5 rounded-2xl bg-white/[0.03] px-5 py-5 ring-1 ring-white/10 max-sm:px-3">
      <h2 className="font-display text-lg font-bold">{existing ? existing.title : 'Yeni sual bankı'}</h2>

      {failure && (
        <p role="alert" data-testid="book-form-failure" className="rounded-2xl bg-bad/10 px-4 py-3 text-sm font-semibold text-fg ring-1 ring-bad/40">
          {failure}
        </p>
      )}

      <Field label="Ad" htmlFor="book-title" errors={errors.title} hint="Kitabın adı və ya kolleksiyanın adı.">
        <input id="book-title" type="text" maxLength={200} className="field min-h-12 w-full rounded-xl px-3" value={form.title}
          aria-invalid={Boolean(errors.title)} onChange={(e) => set('title', e.target.value)} />
      </Field>

      <Field label="Müəllif / mənbə" htmlFor="book-author" errors={errors.author}
        hint="Kitab üçün müəllif; kolleksiya üçün mənbə və ya boş buraxın.">
        <input id="book-author" type="text" maxLength={200} className="field min-h-12 w-full rounded-xl px-3" value={form.author}
          aria-invalid={Boolean(errors.author)} onChange={(e) => set('author', e.target.value)} />
      </Field>

      <Field label="Təsvir" htmlFor="book-description" errors={errors.description}
        hint="Oyunçular bunu kartda görür. Boş qala bilər.">
        <textarea id="book-description" maxLength={2000} rows={4} className="field w-full rounded-xl px-3 py-2" value={form.description}
          aria-invalid={Boolean(errors.description)} onChange={(e) => set('description', e.target.value)} />
      </Field>

      <div className="flex flex-col gap-1.5">
        <span className="text-sm font-bold text-fg-2">Üz qabığı / şəkil</span>
        <CoverPicker value={form.coverImageUrl} onChange={(url) => set('coverImageUrl', url)} onSignedOut={onSignedOut}
          invalid={Boolean(errors.coverImageUrl)} />
        {errors.coverImageUrl?.map((message) => (
          <p key={message} role="alert" className="text-sm font-semibold text-bad">{message}</p>
        ))}
      </div>

      <label className="flex items-start gap-3 rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10">
        <input type="checkbox" className="mt-1 size-5 accent-brand" checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
        <span>
          <span className="block font-bold text-fg">Aktiv</span>
          <span className="block text-sm text-fg-2">Deaktiv bankın kampaniyaları oyunçulara görünmür.</span>
        </span>
      </label>
      {errors.isActive?.map((message) => (
        <p key={message} role="alert" className="-mt-3 text-sm font-semibold text-bad">{message}</p>
      ))}

      <div className="flex flex-wrap gap-3">
        <button type="submit" disabled={saving} className="btn btn-primary min-h-12 px-6" data-testid="book-save">
          {saving ? 'Yadda saxlanılır…' : existing ? 'Yadda saxla' : 'Əlavə et'}
        </button>
        <button type="button" onClick={onCancel} className="btn btn-secondary min-h-12 px-6">Ləğv et</button>
      </div>
    </form>
  )
}

function Field({ label, htmlFor, errors, hint, children }: {
  label: string
  htmlFor: string
  errors?: string[]
  hint?: string
  children: React.ReactNode
}) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={htmlFor} className="text-sm font-bold text-fg-2">{label}</label>
      {children}
      {hint && !errors && <p className="text-xs text-fg-3">{hint}</p>}
      {errors?.map((message) => (
        <p key={message} role="alert" className="text-sm font-semibold text-bad">{message}</p>
      ))}
    </div>
  )
}
