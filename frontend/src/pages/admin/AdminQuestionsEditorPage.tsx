import { useCallback, useEffect, useState } from 'react'
import {
  adminFailure,
  createQuestion,
  deleteQuestion,
  getQuestionOptions,
  getQuestions,
  questionErrors,
  questionInput,
  refusalMessage,
  updateQuestion,
  type AdminQuestion,
  type Difficulty,
  type QuestionFilter,
  type QuestionInput,
  type QuestionOptions,
  type QuestionPage,
} from '../../api/admin'
import ImagePicker from '../../components/admin/ImagePicker'
import { useAdmin } from './adminContext'

const DIFFICULTY: { value: Difficulty; label: string }[] = [
  { value: 'Easy', label: 'Asan' },
  { value: 'Medium', label: 'Orta' },
  { value: 'Hard', label: 'Çətin' },
]

const LETTERS = ['A', 'B', 'C', 'D'] as const

const EMPTY_FILTER: QuestionFilter = { bookId: null, quizModeId: null, difficulty: null, withImage: null, search: '', skip: 0, take: 25 }

function emptyInput(options: QuestionOptions | null): QuestionInput {
  return {
    text: '', optionA: '', optionB: '', optionC: '', optionD: '', correctOption: 'A',
    difficulty: 'Medium', category: '', explanation: '',
    bookId: options?.banks[0]?.id ?? null,
    quizModeId: null,
    imageUrl: '', imageAltText: '', imageSource: '', imageLicense: '',
  }
}

type Editing = { kind: 'new' } | { kind: 'existing'; question: AdminQuestion }

/**
 * /admin/questions-editor: the question banks themselves. Since the seeders stopped reconciling (phase 6),
 * this is the only place a question changes — so it has to be possible to find one among hundreds, see it the
 * way a player will, and be told plainly when something would make it unanswerable.
 */
export default function AdminQuestionsEditorPage() {
  const { onSignedOut } = useAdmin()
  const [options, setOptions] = useState<QuestionOptions | null>(null)
  const [page, setPage] = useState<QuestionPage | null>(null)
  const [filter, setFilter] = useState<QuestionFilter>(EMPTY_FILTER)
  const [search, setSearch] = useState('')
  const [failed, setFailed] = useState(false)
  const [version, setVersion] = useState(0)
  const [editing, setEditing] = useState<Editing | null>(null)
  const [notice, setNotice] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null)
  const [removing, setRemoving] = useState<AdminQuestion | null>(null)

  const fail = useCallback((err: unknown, otherwise: () => void) => {
    const failure = adminFailure(err)
    if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
    else otherwise()
  }, [onSignedOut])

  useEffect(() => {
    const controller = new AbortController()
    getQuestionOptions(controller.signal)
      .then(setOptions)
      .catch((err) => { if (!controller.signal.aborted) fail(err, () => setFailed(true)) })
    return () => controller.abort()
  }, [fail, version])

  // Search as they type, once they pause.
  useEffect(() => {
    const timer = window.setTimeout(() => setFilter((f) => (f.search === search.trim() ? f : { ...f, search: search.trim(), skip: 0 })), 300)
    return () => window.clearTimeout(timer)
  }, [search])

  useEffect(() => {
    const controller = new AbortController()
    getQuestions(filter, controller.signal)
      .then((data) => { setPage(data); setFailed(false) })
      .catch((err) => { if (!controller.signal.aborted) fail(err, () => setFailed(true)) })
    return () => controller.abort()
  }, [filter, version, fail])

  function set<K extends keyof QuestionFilter>(key: K, value: QuestionFilter[K]) {
    setFilter((current) => ({ ...current, [key]: value, skip: 0 }))
  }

  async function remove(question: AdminQuestion) {
    setNotice(null)
    try {
      await deleteQuestion(question.id)
      setRemoving(null)
      setVersion((v) => v + 1)
      setNotice({ tone: 'ok', text: 'Sual silindi. Jurnalda mətni ilə birlikdə saxlanılır.' })
    } catch (err) {
      fail(err, () => setNotice({ tone: 'bad', text: refusalMessage(err) ?? 'Sual silinmədi. Yenidən cəhd edin.' }))
    }
  }

  const shown = page?.questions ?? []
  const from = (page?.skip ?? 0) + 1
  const to = (page?.skip ?? 0) + shown.length

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="questions-title">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="questions-title" className="font-display text-2xl font-bold">Suallar</h1>
          <p className="mt-1 max-w-[62ch] text-sm text-fg-2">
            Sualları axtarın, düzəldin, yenisini yazın. Hər sual bir banka və bir kateqoriyaya bağlıdır — oyun raundu buradan yığır.
          </p>
        </div>
        {!editing && options && (
          <button type="button" onClick={() => { setNotice(null); setEditing({ kind: 'new' }) }} className="btn btn-primary min-h-11 px-5 text-sm">
            Yeni sual
          </button>
        )}
      </div>

      {notice && (
        <p role={notice.tone === 'bad' ? 'alert' : 'status'} data-testid="questions-notice"
          className={`mt-5 rounded-2xl px-4 py-3 text-sm font-semibold ring-1 ${notice.tone === 'bad' ? 'bg-bad/10 text-fg ring-bad/40' : 'bg-ok/10 text-fg ring-ok/30'}`}>
          {notice.text}
        </p>
      )}

      {editing && options && (
        <QuestionForm
          key={editing.kind === 'new' ? 'new' : editing.question.id}
          options={options}
          initial={editing.kind === 'new' ? emptyInput(options) : questionInput(editing.question)}
          existing={editing.kind === 'new' ? null : editing.question}
          onCancel={() => setEditing(null)}
          onSaved={(question, wasNew) => {
            setEditing(null)
            setVersion((v) => v + 1)
            setNotice({ tone: 'ok', text: wasNew ? 'Sual əlavə olundu.' : `#${question.id} yadda saxlanıldı.` })
          }}
          onSignedOut={onSignedOut}
        />
      )}

      {/* --- filters --- */}
      {options && (
        <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4" data-testid="question-filters">
          <Labelled label="Bank" htmlFor="filter-bank">
            <select id="filter-bank" className="field min-h-11 w-full rounded-xl px-3" value={filter.bookId ?? ''}
              onChange={(e) => set('bookId', e.target.value ? Number(e.target.value) : null)}>
              <option value="">Hamısı</option>
              {options.banks.map((bank) => (
                <option key={bank.id} value={bank.id}>{bank.title} ({bank.easy + bank.medium + bank.hard})</option>
              ))}
            </select>
          </Labelled>
          <Labelled label="Kateqoriya" htmlFor="filter-mode">
            <select id="filter-mode" className="field min-h-11 w-full rounded-xl px-3" value={filter.quizModeId ?? ''}
              onChange={(e) => set('quizModeId', e.target.value ? Number(e.target.value) : null)}>
              <option value="">Hamısı</option>
              {options.quizModes.map((mode) => <option key={mode.id} value={mode.id}>{mode.title}</option>)}
            </select>
          </Labelled>
          <Labelled label="Çətinlik" htmlFor="filter-difficulty">
            <select id="filter-difficulty" className="field min-h-11 w-full rounded-xl px-3" value={filter.difficulty ?? ''}
              onChange={(e) => set('difficulty', (e.target.value || null) as Difficulty | null)}>
              <option value="">Hamısı</option>
              {DIFFICULTY.map((d) => <option key={d.value} value={d.value}>{d.label}</option>)}
            </select>
          </Labelled>
          <Labelled label="Şəkil" htmlFor="filter-image">
            <select id="filter-image" className="field min-h-11 w-full rounded-xl px-3"
              value={filter.withImage === null ? '' : filter.withImage ? 'yes' : 'no'}
              onChange={(e) => set('withImage', e.target.value === '' ? null : e.target.value === 'yes')}>
              <option value="">Hamısı</option>
              <option value="yes">Şəkilli</option>
              <option value="no">Şəkilsiz</option>
            </select>
          </Labelled>
          <div className="sm:col-span-2 lg:col-span-4">
            <Labelled label="Axtarış" htmlFor="filter-search">
              <input id="filter-search" type="search" className="field min-h-11 w-full rounded-xl px-3"
                placeholder="Sual mətni, variant və ya alt kateqoriya" value={search} onChange={(e) => setSearch(e.target.value)} />
            </Labelled>
          </div>
        </div>
      )}

      {failed && <p role="alert" className="mt-6 font-semibold text-fg">Sualları yükləmək alınmadı.</p>}
      {!page && !failed && <p role="status" className="mt-6 text-fg-2">Yüklənir…</p>}

      {page && (
        <>
          <p className="mt-5 text-sm font-semibold text-fg-3" data-testid="question-total">
            {page.total === 0 ? 'Uyğun sual yoxdur.' : `${page.total} sualdan ${from}–${to} göstərilir.`}
          </p>

          <ul className="mt-3 flex flex-col gap-2" data-testid="question-list">
            {shown.map((question) => (
              <li key={question.id} data-testid={`question-${question.id}`} className="rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10">
                <div className="flex flex-wrap items-start gap-3">
                  {question.imageUrl && (
                    <img src={question.imageUrl} alt="" loading="lazy" className="h-16 w-24 shrink-0 rounded-lg bg-ink-950 object-cover" />
                  )}
                  <div className="min-w-[12rem] flex-1">
                    <p className="font-semibold text-fg">{question.text}</p>
                    <p className="mt-1 text-xs text-fg-3">
                      {DIFFICULTY.find((d) => d.value === question.difficulty)?.label} · {question.category}
                      {question.bookTitle ? ` · ${question.bookTitle}` : ''}
                      {question.quizModeTitle ? ` · ${question.quizModeTitle}` : ' · kateqoriyasız'}
                      {' · '}düzgün: {question.correctOption}
                    </p>
                  </div>
                  <div className="flex gap-2">
                    <button type="button" className="btn btn-secondary min-h-10 px-3 text-xs"
                      onClick={() => { setNotice(null); setRemoving(null); setEditing({ kind: 'existing', question }) }}>
                      Düzəlt
                    </button>
                    <button type="button" className="btn btn-secondary min-h-10 px-3 text-xs"
                      onClick={() => { setNotice(null); setRemoving(question) }}>
                      Sil
                    </button>
                  </div>
                </div>

                {removing?.id === question.id && (
                  <div className="mt-3 rounded-xl bg-bad/10 px-4 py-3 ring-1 ring-bad/40" data-testid="question-confirm">
                    <p className="text-sm font-semibold text-fg">
                      Bu sual silinsin? Geri qaytarmaq olmaz — mətni Jurnalda qalır.
                    </p>
                    <div className="mt-3 flex flex-wrap gap-2">
                      <button type="button" onClick={() => void remove(question)} className="btn btn-primary min-h-10 px-4 text-sm" data-testid="question-confirm-delete">
                        Sil
                      </button>
                      <button type="button" onClick={() => setRemoving(null)} className="btn btn-secondary min-h-10 px-4 text-sm">Ləğv et</button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>

          {page.total > page.take && (
            <div className="mt-4 flex flex-wrap items-center gap-3">
              <button type="button" disabled={page.skip === 0} className="btn btn-secondary min-h-10 px-4 text-sm"
                onClick={() => setFilter((f) => ({ ...f, skip: Math.max(0, f.skip - f.take) }))}>
                Əvvəlki
              </button>
              <button type="button" disabled={to >= page.total} className="btn btn-secondary min-h-10 px-4 text-sm"
                onClick={() => setFilter((f) => ({ ...f, skip: f.skip + f.take }))}>
                Növbəti
              </button>
            </div>
          )}
        </>
      )}
    </section>
  )
}

function Labelled({ label, htmlFor, children }: { label: string; htmlFor: string; children: React.ReactNode }) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={htmlFor} className="text-sm font-bold text-fg-2">{label}</label>
      {children}
    </div>
  )
}

function QuestionForm({ options, initial, existing, onCancel, onSaved, onSignedOut }: {
  options: QuestionOptions
  initial: QuestionInput
  existing: AdminQuestion | null
  onCancel: () => void
  onSaved: (question: AdminQuestion, wasNew: boolean) => void
  onSignedOut: () => void
}) {
  const [form, setForm] = useState<QuestionInput>(initial)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  function set<K extends keyof QuestionInput>(key: K, value: QuestionInput[K]) {
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
      const saved = existing ? await updateQuestion(existing.id, form) : await createQuestion(form)
      onSaved(saved, existing === null)
    } catch (err) {
      const fieldErrors = questionErrors(err)
      if (fieldErrors) {
        setErrors(fieldErrors)
        setFailure('Sual yadda saxlanmadı. Qırmızı ilə göstərilənləri düzəldin.')
      } else {
        const kind = adminFailure(err)
        if (kind === 'signed-out' || kind === 'not-an-admin') onSignedOut()
        else setFailure('Serverlə əlaqə alınmadı. Yenidən cəhd edin.')
      }
    } finally {
      setSaving(false)
    }
  }

  const answer = form[`option${form.correctOption}` as 'optionA'] ?? ''

  return (
    <form onSubmit={(e) => void submit(e)} noValidate data-testid="question-form"
      className="mt-6 flex flex-col gap-5 rounded-2xl bg-white/[0.03] px-5 py-5 ring-1 ring-white/10 max-sm:px-3">
      <h2 className="font-display text-lg font-bold">{existing ? `Sual #${existing.id}` : 'Yeni sual'}</h2>

      {failure && (
        <p role="alert" data-testid="question-form-failure" className="rounded-2xl bg-bad/10 px-4 py-3 text-sm font-semibold text-fg ring-1 ring-bad/40">
          {failure}
        </p>
      )}

      <div className="grid gap-5 lg:grid-cols-[3fr_2fr]">
        <div className="flex min-w-0 flex-col gap-5">
          <Field label="Sual" htmlFor="q-text" errors={errors.text}>
            <textarea id="q-text" rows={3} maxLength={1000} className="field w-full rounded-xl px-3 py-2" value={form.text}
              aria-invalid={Boolean(errors.text)} onChange={(e) => set('text', e.target.value)} />
          </Field>

          <fieldset className="flex flex-col gap-3">
            <legend className="text-sm font-bold text-fg-2">Variantlar — düzgün olanı seçin</legend>
            {LETTERS.map((letter) => {
              const key = `option${letter}` as 'optionA'
              return (
                <div key={letter} className="flex flex-col gap-1">
                  <div className="flex items-center gap-2">
                    <label className="flex min-h-11 w-11 shrink-0 cursor-pointer items-center justify-center rounded-xl bg-white/[0.06] font-display font-bold ring-1 ring-white/10">
                      <input type="radio" name="correct" value={letter} checked={form.correctOption === letter} className="sr-only"
                        onChange={() => set('correctOption', letter)} />
                      <span className={form.correctOption === letter ? 'text-ok' : 'text-fg-3'}>{letter}</span>
                    </label>
                    <input type="text" maxLength={300} aria-label={`Variant ${letter}`} value={form[key]}
                      aria-invalid={Boolean(errors[key])} onChange={(e) => set(key, e.target.value)}
                      className={`field min-h-11 w-full rounded-xl px-3 ${form.correctOption === letter ? 'ring-1 ring-ok/40' : ''}`} />
                  </div>
                  {errors[key]?.map((message) => (
                    <p key={message} role="alert" className="pl-13 text-sm font-semibold text-bad">{message}</p>
                  ))}
                </div>
              )
            })}
            {errors.correctOption?.map((message) => (
              <p key={message} role="alert" className="text-sm font-semibold text-bad">{message}</p>
            ))}
          </fieldset>

          <div className="grid gap-5 sm:grid-cols-2">
            <Field label="Çətinlik" htmlFor="q-difficulty" errors={errors.difficulty}>
              <select id="q-difficulty" className="field min-h-12 w-full rounded-xl px-3" value={form.difficulty}
                onChange={(e) => set('difficulty', e.target.value as Difficulty)}>
                {DIFFICULTY.map((d) => <option key={d.value} value={d.value}>{d.label}</option>)}
              </select>
            </Field>
            <Field label="Alt kateqoriya" htmlFor="q-category" errors={errors.category} hint="Məsələn: Bayraqlar, Bitkilər.">
              <input id="q-category" list="q-categories" maxLength={100} className="field min-h-12 w-full rounded-xl px-3"
                value={form.category} aria-invalid={Boolean(errors.category)} onChange={(e) => set('category', e.target.value)} />
              <datalist id="q-categories">
                {options.categories.map((category) => <option key={category} value={category} />)}
              </datalist>
            </Field>
          </div>

          <div className="grid gap-5 sm:grid-cols-2">
            <Field label="Bank" htmlFor="q-book" errors={errors.bookId}>
              <select id="q-book" className="field min-h-12 w-full rounded-xl px-3" value={form.bookId ?? ''}
                aria-invalid={Boolean(errors.bookId)} onChange={(e) => set('bookId', e.target.value ? Number(e.target.value) : null)}>
                <option value="">Seçin</option>
                {options.banks.map((bank) => <option key={bank.id} value={bank.id}>{bank.title}</option>)}
              </select>
            </Field>
            <Field label="Kateqoriya" htmlFor="q-mode" errors={errors.quizModeId} hint="Sual bu kateqoriyanın raundunda çıxır.">
              <select id="q-mode" className="field min-h-12 w-full rounded-xl px-3" value={form.quizModeId ?? ''}
                aria-invalid={Boolean(errors.quizModeId)} onChange={(e) => set('quizModeId', e.target.value ? Number(e.target.value) : null)}>
                <option value="">Seçin</option>
                {options.quizModes.map((mode) => <option key={mode.id} value={mode.id}>{mode.title}</option>)}
              </select>
            </Field>
          </div>

          <Field label="İzah" htmlFor="q-explanation" errors={errors.explanation} hint="Cavabdan sonra göstərilir. Boş qala bilər.">
            <textarea id="q-explanation" rows={2} maxLength={2000} className="field w-full rounded-xl px-3 py-2" value={form.explanation}
              onChange={(e) => set('explanation', e.target.value)} />
          </Field>
        </div>

        <div className="flex min-w-0 flex-col gap-5">
          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-bold text-fg-2">Şəkil (istəyə bağlı)</span>
            <ImagePicker kind="question" value={form.imageUrl} onChange={(url) => set('imageUrl', url)}
              onSignedOut={onSignedOut} invalid={Boolean(errors.imageUrl)} />
            {errors.imageUrl?.map((message) => (
              <p key={message} role="alert" className="text-sm font-semibold text-bad">{message}</p>
            ))}
          </div>

          {form.imageUrl && (
            <>
              <Field label="Alt mətn" htmlFor="q-alt" errors={errors.imageAltText}
                hint="Şəkli görməyən üçün izah. Düzgün cavabı deməməlidir.">
                <input id="q-alt" type="text" maxLength={300} className="field min-h-12 w-full rounded-xl px-3" value={form.imageAltText}
                  aria-invalid={Boolean(errors.imageAltText)} onChange={(e) => set('imageAltText', e.target.value)} />
              </Field>
              <Field label="Mənbə" htmlFor="q-source" errors={errors.imageSource}>
                <input id="q-source" type="text" maxLength={300} className="field min-h-12 w-full rounded-xl px-3" value={form.imageSource}
                  aria-invalid={Boolean(errors.imageSource)} onChange={(e) => set('imageSource', e.target.value)} />
              </Field>
              <Field label="Lisenziya" htmlFor="q-licence" errors={errors.imageLicense}
                hint="Öz şəklimizdirsə: “Şirkətin öz şəkli”.">
                <input id="q-licence" type="text" maxLength={300} className="field min-h-12 w-full rounded-xl px-3" value={form.imageLicense}
                  aria-invalid={Boolean(errors.imageLicense)} onChange={(e) => set('imageLicense', e.target.value)} />
              </Field>
              <div className="flex flex-wrap gap-2">
                <button type="button" className="btn btn-secondary min-h-10 px-3 text-xs"
                  onClick={() => { set('imageSource', 'Öz arxivimiz'); set('imageLicense', 'Şirkətin öz şəkli') }}>
                  Öz şəklimizdir
                </button>
              </div>
            </>
          )}

          {/* What the player will see. The point of the editor is that this is not a surprise. */}
          <div className="rounded-2xl bg-ink-950/60 px-4 py-4 ring-1 ring-white/10" data-testid="question-preview">
            <p className="text-xs font-bold uppercase tracking-wider text-fg-3">Oyunçunun gördüyü</p>
            {form.imageUrl && (
              <img src={form.imageUrl} alt={form.imageAltText} className="mt-3 max-h-40 w-full rounded-xl bg-ink-950 object-contain" />
            )}
            <p className="mt-3 font-display text-base font-bold text-fg">{form.text || 'Sual mətni'}</p>
            <ul className="mt-3 grid gap-2 sm:grid-cols-2">
              {LETTERS.map((letter) => (
                <li key={letter}
                  className={`rounded-xl px-3 py-2 text-sm font-semibold ring-1 ${form.correctOption === letter ? 'bg-ok/10 text-fg ring-ok/40' : 'bg-white/[0.04] text-fg-2 ring-white/10'}`}>
                  <span className="font-display font-bold">{letter}.</span> {form[`option${letter}` as 'optionA'] || '—'}
                </li>
              ))}
            </ul>
            <p className="mt-3 text-xs text-fg-3">Düzgün cavab: {form.correctOption} — {answer || '—'}</p>
          </div>
        </div>
      </div>

      <div className="flex flex-wrap gap-3">
        <button type="submit" disabled={saving} className="btn btn-primary min-h-12 px-6" data-testid="question-save">
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
