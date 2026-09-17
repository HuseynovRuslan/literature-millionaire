import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import {
  adminFailure,
  campaignErrors,
  campaignInput,
  createCampaign,
  getCampaignOptions,
  getCampaigns,
  updateCampaign,
  type AdminCampaign,
  type CampaignInput,
  type CampaignOptions,
} from '../../api/admin'
import { useAdmin } from './adminContext'
import { dayCount, formatDay, nextPeriod } from './campaignDates'

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; options: CampaignOptions; editing: AdminCampaign | null; initial: CampaignInput; from: AdminCampaign | null }
  | { kind: 'missing' }
  | { kind: 'error' }

/**
 * /admin/campaigns/new, /admin/campaigns/new?from=<id> (the next period of that campaign, same settings) and
 * /admin/campaigns/<id>. The server decides whether a campaign can be saved; this page shows what it would be
 * built from, so the answer is rarely a surprise.
 */
export default function AdminCampaignEditorPage() {
  const { onSignedOut } = useAdmin()
  const { id } = useParams()
  const [searchParams] = useSearchParams()
  const editId = id ? Number(id) : null
  const fromId = searchParams.get('from') ? Number(searchParams.get('from')) : null
  const [load, setLoad] = useState<Load>({ kind: 'loading' })

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([getCampaignOptions(controller.signal), getCampaigns(controller.signal)])
      .then(([options, campaigns]) => {
        const editing = editId === null ? null : campaigns.find((c) => c.id === editId) ?? null
        const from = fromId === null ? null : campaigns.find((c) => c.id === fromId) ?? null
        if ((editId !== null && !editing) || (fromId !== null && !from)) {
          setLoad({ kind: 'missing' })
          return
        }
        setLoad({ kind: 'ready', options, editing, from, initial: initialInput(options, editing, from) })
      })
      .catch((err) => {
        if (controller.signal.aborted) return
        const failure = adminFailure(err)
        if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
        else setLoad({ kind: 'error' })
      })
    return () => controller.abort()
  }, [editId, fromId, onSignedOut])

  const title = editId !== null ? `Kampaniya #${editId}` : fromId !== null ? 'Növbəti dövr' : 'Yeni kampaniya'

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="campaign-editor-title">
      <Link to="/admin/campaigns" className="text-sm font-semibold text-fg-3 hover:text-fg">← Kampaniyalar</Link>
      <h1 id="campaign-editor-title" className="mt-2 font-display text-2xl font-bold">{title}</h1>

      {load.kind === 'loading' && <p role="status" className="mt-6 text-fg-2">Yüklənir…</p>}
      {load.kind === 'error' && <p role="alert" className="mt-6 font-semibold text-fg">Məlumatı yükləmək alınmadı.</p>}
      {load.kind === 'missing' && <p role="alert" className="mt-6 font-semibold text-fg">Belə kampaniya yoxdur.</p>}
      {load.kind === 'ready' && (
        <>
          {load.from && (
            <p className="mt-2 text-sm text-fg-2">
              #{load.from.id} ({load.from.quizModeTitle}, {formatDay(load.from.startDate)} – {formatDay(load.from.endDate)}) kampaniyasının
              ayarları köçürüldü, tarixlər növbəti dövrə keçirildi. Oyunçuların cəhdləri yeni kampaniyada sıfırdan başlayır.
            </p>
          )}
          <CampaignForm options={load.options} editing={load.editing} initial={load.initial} onSignedOut={onSignedOut} />
        </>
      )}
    </section>
  )
}

function initialInput(options: CampaignOptions, editing: AdminCampaign | null, from: AdminCampaign | null): CampaignInput {
  if (editing) return campaignInput(editing)
  if (from) return { ...campaignInput(from), ...nextPeriod(from.startDate, from.endDate), isEnabled: true }
  return {
    quizModeId: null,
    bookId: null,
    startDate: options.today,
    endDate: options.today,
    passingScore: 8,
    rewardTitle: '',
    imageQuestionsPerQuiz: 2,
    isEnabled: true,
  }
}

function CampaignForm({ options, editing, initial, onSignedOut }: {
  options: CampaignOptions
  editing: AdminCampaign | null
  initial: CampaignInput
  onSignedOut: () => void
}) {
  const navigate = useNavigate()
  const [form, setForm] = useState<CampaignInput>(initial)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const mode = options.quizModes.find((m) => m.id === form.quizModeId) ?? null
  // Changing the category or book of a campaign that has been played would put results under other questions.
  const locked = (editing?.attemptsStarted ?? 0) > 0

  // A category played per book (Ayın Kitabı) offers every book, because the month's book is added first and
  // its questions come after. Elsewhere the list is the books that actually have questions in this category,
  // plus whichever one is already chosen.
  const bookIds = new Set(options.pools.filter((p) => p.quizModeId === form.quizModeId && p.bookId !== null).map((p) => p.bookId))
  if (form.bookId !== null) bookIds.add(form.bookId)
  const books = mode?.requiresBook ? options.books : options.books.filter((b) => bookIds.has(b.id))

  const pools = options.pools.filter((p) => p.quizModeId === form.quizModeId && (form.bookId === null || p.bookId === form.bookId))
  const bank = pools.reduce(
    (sum, p) => ({ easy: sum.easy + p.easy, medium: sum.medium + p.medium, hard: sum.hard + p.hard, images: sum.images + p.images }),
    { easy: 0, medium: 0, hard: 0, images: 0 },
  )
  const bankFills = bank.easy >= options.easyPerQuiz && bank.medium >= options.mediumPerQuiz && bank.hard >= options.hardPerQuiz

  function set<K extends keyof CampaignInput>(key: K, value: CampaignInput[K]) {
    setForm((current) => ({ ...current, [key]: value }))
    setErrors((current) => {
      if (!(key in current)) return current
      const next = { ...current }
      delete next[key]
      return next
    })
  }

  function chooseMode(value: string) {
    const quizModeId = value ? Number(value) : null
    const modeBooks = options.pools.filter((p) => p.quizModeId === quizModeId && p.bookId !== null)
    // One book in the category: that is the bank. Otherwise let the admin choose (or play the whole category).
    const bookId = modeBooks.length === 1 ? modeBooks[0].bookId : null
    setForm((current) => ({ ...current, quizModeId, bookId }))
    setErrors({})
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setSaving(true)
    setFailure(null)
    try {
      const saved = editing ? await updateCampaign(editing.id, form) : await createCampaign(form)
      navigate('/admin/campaigns', {
        replace: true,
        state: { saved: editing ? `#${saved.id} yadda saxlanıldı.` : `#${saved.id} yaradıldı: ${saved.quizModeTitle}, ${formatDay(saved.startDate)} – ${formatDay(saved.endDate)}.` },
      })
    } catch (err) {
      const fieldErrors = campaignErrors(err)
      if (fieldErrors) {
        setErrors(fieldErrors)
        setFailure('Kampaniya yadda saxlanmadı. Qırmızı ilə göstərilənləri düzəldin.')
      } else {
        const kind = adminFailure(err)
        if (kind === 'signed-out' || kind === 'not-an-admin') onSignedOut()
        else setFailure('Serverlə əlaqə alınmadı. Yenidən cəhd edin.')
      }
    } finally {
      setSaving(false)
    }
  }

  const days = form.startDate && form.endDate && form.endDate >= form.startDate ? dayCount(form.startDate, form.endDate) : null

  return (
    <form onSubmit={(e) => void submit(e)} noValidate className="mt-6 flex max-w-[44rem] flex-col gap-5" data-testid="campaign-form">
      {failure && (
        <p role="alert" className="rounded-2xl bg-bad/10 px-4 py-3 text-sm font-semibold text-fg ring-1 ring-bad/40" data-testid="campaign-form-failure">
          {failure}
        </p>
      )}

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label="Kateqoriya" errors={errors.quizModeId} htmlFor="campaign-mode">
          <select id="campaign-mode" className="field min-h-12 w-full rounded-xl px-3" value={form.quizModeId ?? ''} disabled={locked}
            aria-invalid={Boolean(errors.quizModeId)} onChange={(e) => chooseMode(e.target.value)}>
            <option value="">Seçin</option>
            {options.quizModes.map((m) => (
              <option key={m.id} value={m.id}>{m.title}{m.isActive ? '' : ' (deaktiv)'}</option>
            ))}
          </select>
        </Field>

        <Field label="Kitab / sual bankı" errors={errors.bookId} htmlFor="campaign-book">
          <select id="campaign-book" className="field min-h-12 w-full rounded-xl px-3" value={form.bookId ?? ''}
            disabled={locked || form.quizModeId === null} aria-invalid={Boolean(errors.bookId)}
            onChange={(e) => set('bookId', e.target.value ? Number(e.target.value) : null)}>
            {(!mode || !mode.requiresBook) && <option value="">Kateqoriyanın bütün sualları</option>}
            {mode?.requiresBook && <option value="">Seçin</option>}
            {books.map((b) => (
              <option key={b.id} value={b.id}>{b.title}{b.isActive ? '' : ' (deaktiv)'}</option>
            ))}
          </select>
        </Field>
      </div>

      {locked && (
        <p className="-mt-2 text-sm text-fg-3">Bu kampaniyada artıq {editing!.attemptsStarted} cəhd var: kateqoriya və kitab dəyişdirilə bilməz.</p>
      )}

      {form.quizModeId !== null && (
        <div data-testid="campaign-bank"
          className={`rounded-2xl px-4 py-3 text-sm ring-1 ${bankFills ? 'bg-ok/10 ring-ok/30' : 'bg-bad/10 ring-bad/40'}`}>
          <p className="font-semibold text-fg">
            {bankFills ? 'Sual bankı raundu doldurur.' : 'Sual bankı raundu doldurmur: bu halda kampaniyanı aktiv etmək olmaz.'}
          </p>
          <p className="mt-1 tabular-nums text-fg-2">
            Asan {bank.easy} (lazım {options.easyPerQuiz}) · Orta {bank.medium} (lazım {options.mediumPerQuiz}) · Çətin {bank.hard} (lazım {options.hardPerQuiz}) · Şəkilli {bank.images}
          </p>
        </div>
      )}

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label="Başlama tarixi" errors={errors.startDate} htmlFor="campaign-start">
          <input id="campaign-start" type="date" className="field min-h-12 w-full rounded-xl px-3" value={form.startDate}
            aria-invalid={Boolean(errors.startDate)} onChange={(e) => set('startDate', e.target.value)} />
        </Field>
        <Field label="Bitmə tarixi" errors={errors.endDate} htmlFor="campaign-end"
          hint={days ? `${days} gün, son gün daxil. Bugün: ${formatDay(options.today)}.` : undefined}>
          <input id="campaign-end" type="date" className="field min-h-12 w-full rounded-xl px-3" value={form.endDate}
            aria-invalid={Boolean(errors.endDate)} onChange={(e) => set('endDate', e.target.value)} />
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label="Keçid balı (10 sualdan düzgün cavab)" errors={errors.passingScore} htmlFor="campaign-pass">
          <input id="campaign-pass" type="number" min={1} max={10} inputMode="numeric" className="field min-h-12 w-full rounded-xl px-3"
            value={form.passingScore} aria-invalid={Boolean(errors.passingScore)}
            onChange={(e) => set('passingScore', Number(e.target.value))} />
        </Field>
        <Field label="Raundda şəkilli sual" errors={errors.imageQuestionsPerQuiz} htmlFor="campaign-images"
          hint="Bankda az olsa, oyun olanı istifadə edir.">
          <input id="campaign-images" type="number" min={0} max={10} inputMode="numeric" className="field min-h-12 w-full rounded-xl px-3"
            value={form.imageQuestionsPerQuiz} aria-invalid={Boolean(errors.imageQuestionsPerQuiz)}
            onChange={(e) => set('imageQuestionsPerQuiz', Number(e.target.value))} />
        </Field>
      </div>

      <Field label="Mükafat" errors={errors.rewardTitle} htmlFor="campaign-reward">
        <input id="campaign-reward" type="text" maxLength={200} className="field min-h-12 w-full rounded-xl px-3" value={form.rewardTitle}
          aria-invalid={Boolean(errors.rewardTitle)} onChange={(e) => set('rewardTitle', e.target.value)} />
      </Field>

      <label className="flex items-start gap-3 rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10">
        <input type="checkbox" className="mt-1 size-5 accent-brand" checked={form.isEnabled} onChange={(e) => set('isEnabled', e.target.checked)} />
        <span>
          <span className="block font-bold text-fg">Aktiv</span>
          <span className="block text-sm text-fg-2">Aktiv kampaniya öz tarixlərində oyunçulara görünür. Deaktiv kampaniya qaralama kimi saxlanır.</span>
        </span>
      </label>

      <div className="flex flex-wrap gap-3">
        <button type="submit" disabled={saving} className="btn btn-primary min-h-12 px-6" data-testid="campaign-save">
          {saving ? 'Yadda saxlanılır…' : editing ? 'Yadda saxla' : 'Yarat'}
        </button>
        <Link to="/admin/campaigns" className="btn btn-secondary min-h-12 px-6">Ləğv et</Link>
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
