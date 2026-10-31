import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  adminFailure,
  categoryErrors,
  categoryInput,
  getCategories,
  getCategoryIcons,
  moveCategory,
  updateCategory,
  type AdminCategory,
  type CategoryInput,
} from '../../api/admin'
import QuizModeIcon from '../../components/home/QuizModeIcon'
import { useAdmin } from './adminContext'

/**
 * /admin/categories: the tiles on the home page, in the order players see them.
 *
 * Adding or removing a category is not here. A category is what questions, campaigns and the card artwork all
 * key on, so a new one is a change to the product rather than a form; one that should no longer be offered is
 * switched off, and everything played in it stays readable.
 */
export default function AdminCategoriesPage() {
  const { onSignedOut } = useAdmin()
  const [categories, setCategories] = useState<AdminCategory[] | null>(null)
  const [icons, setIcons] = useState<string[]>([])
  const [failed, setFailed] = useState(false)
  const [editing, setEditing] = useState<AdminCategory | null>(null)
  const [notice, setNotice] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null)
  const [busy, setBusy] = useState(false)

  const fail = useCallback((err: unknown, otherwise: () => void) => {
    const failure = adminFailure(err)
    if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
    else otherwise()
  }, [onSignedOut])

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([getCategories(controller.signal), getCategoryIcons(controller.signal)])
      .then(([list, iconKeys]) => { setCategories(list); setIcons(iconKeys); setFailed(false) })
      .catch((err) => { if (!controller.signal.aborted) fail(err, () => setFailed(true)) })
    return () => controller.abort()
  }, [fail])

  async function move(category: AdminCategory, direction: 'up' | 'down') {
    setBusy(true)
    setNotice(null)
    try {
      setCategories(await moveCategory(category.id, direction))
    } catch (err) {
      fail(err, () => setNotice({ tone: 'bad', text: 'Sıranı dəyişmək alınmadı.' }))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="categories-title">
      <h1 id="categories-title" className="font-display text-2xl font-bold">Kateqoriyalar</h1>
      <p className="mt-1 max-w-[66ch] text-sm text-fg-2">
        Ana səhifədəki kartlar: adı, təsviri, nişanı, sırası. "Test versiya" nişanı bankı hazır olmayan kateqoriya üçündür —
        oyunçu yarımçıq bankı xəbərdarlıqsız görəndə bunu nasaz məhsul kimi başa düşür. Yeni kateqoriya buradan açılmır:
        o, sualların və kampaniyaların bağlandığı bir şeydir. Silinmir də — lazım olmayanı deaktiv edin.
      </p>

      {notice && (
        <p role={notice.tone === 'bad' ? 'alert' : 'status'} data-testid="categories-notice"
          className={`mt-5 rounded-2xl px-4 py-3 text-sm font-semibold ring-1 ${notice.tone === 'bad' ? 'bg-bad/10 text-fg ring-bad/40' : 'bg-ok/10 text-fg ring-ok/30'}`}>
          {notice.text}
        </p>
      )}

      {failed && <p role="alert" className="mt-6 font-semibold text-fg">Kateqoriyaları yükləmək alınmadı.</p>}
      {!categories && !failed && <p role="status" className="mt-6 text-fg-2">Yüklənir…</p>}

      {categories && (
        <ul className="mt-6 flex flex-col gap-3" data-testid="category-list">
          {categories.map((category, index) => (
            <li key={category.id} data-testid={`category-${category.id}`}
              className={`rounded-2xl px-4 py-4 ring-1 ${category.isActive ? 'bg-white/[0.04] ring-white/10' : 'bg-white/[0.02] ring-white/[0.06]'}`}>
              <div className="flex flex-wrap items-start gap-4">
                <span className="grid size-12 shrink-0 place-items-center rounded-2xl bg-white/[0.06] text-fg-2 ring-1 ring-white/10">
                  <QuizModeIcon iconKey={category.iconKey} className="size-6" />
                </span>

                <div className="min-w-[12rem] flex-1">
                  <p className="flex flex-wrap items-center gap-2">
                    <span className="font-display text-lg font-bold text-fg">{category.title}</span>
                    {!category.isActive && (
                      <span className="rounded-full bg-white/[0.06] px-2.5 py-0.5 text-xs font-bold text-fg-3 ring-1 ring-white/15">Deaktiv</span>
                    )}
                    {category.isPreview && (
                      <span className="rounded-full bg-sun/15 px-2.5 py-0.5 text-xs font-bold text-sun ring-1 ring-sun/40">Test versiya</span>
                    )}
                    {category.requiresBook && (
                      <span className="rounded-full bg-white/[0.06] px-2.5 py-0.5 text-xs font-bold text-fg-3 ring-1 ring-white/15">Kitab üzrə</span>
                    )}
                  </p>
                  <p className="mt-0.5 text-sm text-fg-2">{category.description}</p>
                  <p className="mt-2 text-xs font-semibold text-fg-3">
                    {category.questionCount} sual · {category.campaignCount} kampaniya
                    {category.hasRunningCampaign ? ' · indi oynanılır' : ''} · sıra {category.displayOrder}
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <button type="button" onClick={() => void move(category, 'up')} disabled={busy || index === 0}
                    aria-label={`${category.title}: yuxarı`} className="btn btn-secondary min-h-10 px-3 text-sm">↑</button>
                  <button type="button" onClick={() => void move(category, 'down')} disabled={busy || index === categories.length - 1}
                    aria-label={`${category.title}: aşağı`} className="btn btn-secondary min-h-10 px-3 text-sm">↓</button>
                  <button type="button" onClick={() => { setNotice(null); setEditing(category) }}
                    className="btn btn-secondary min-h-10 px-4 text-sm">Düzəlt</button>
                </div>
              </div>

              {editing?.id === category.id && (
                <CategoryForm
                  key={category.id}
                  category={category}
                  icons={icons}
                  onCancel={() => setEditing(null)}
                  onSaved={(saved) => {
                    setCategories((current) => (current ?? []).map((c) => (c.id === saved.id ? saved : c)))
                    setEditing(null)
                    setNotice({ tone: 'ok', text: `"${saved.title}" yadda saxlanıldı.` })
                  }}
                  onSignedOut={onSignedOut}
                />
              )}
            </li>
          ))}
        </ul>
      )}

      <p className="mt-6 text-sm text-fg-3">
        Kateqoriyanın kampaniyaları <Link to="/admin/campaigns" className="underline decoration-white/30 hover:text-fg">Kampaniyalar</Link>,
        sualları <Link to="/admin/questions-editor" className="underline decoration-white/30 hover:text-fg">Suallar</Link> bölməsindədir.
      </p>
    </section>
  )
}

function CategoryForm({ category, icons, onCancel, onSaved, onSignedOut }: {
  category: AdminCategory
  icons: string[]
  onCancel: () => void
  onSaved: (category: AdminCategory) => void
  onSignedOut: () => void
}) {
  const [form, setForm] = useState<CategoryInput>(categoryInput(category))
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  function set<K extends keyof CategoryInput>(key: K, value: CategoryInput[K]) {
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
      onSaved(await updateCategory(category.id, form))
    } catch (err) {
      const fieldErrors = categoryErrors(err)
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
    <form onSubmit={(e) => void submit(e)} noValidate data-testid="category-form"
      className="mt-4 flex max-w-[44rem] flex-col gap-5 rounded-2xl bg-white/[0.03] px-5 py-5 ring-1 ring-white/10 max-sm:px-3">
      <p className="text-xs font-semibold text-fg-3">Açar: {category.slug} — dəyişmir, suallar və kampaniyalar ona bağlıdır.</p>

      {failure && (
        <p role="alert" data-testid="category-form-failure" className="rounded-2xl bg-bad/10 px-4 py-3 text-sm font-semibold text-fg ring-1 ring-bad/40">
          {failure}
        </p>
      )}

      <Field label="Ad" htmlFor={`cat-title-${category.id}`} errors={errors.title}>
        <input id={`cat-title-${category.id}`} type="text" maxLength={120} className="field min-h-12 w-full rounded-xl px-3"
          value={form.title} aria-invalid={Boolean(errors.title)} onChange={(e) => set('title', e.target.value)} />
      </Field>

      <Field label="Təsvir" htmlFor={`cat-desc-${category.id}`} errors={errors.description} hint="Kartda adın altında görünür.">
        <textarea id={`cat-desc-${category.id}`} rows={2} maxLength={500} className="field w-full rounded-xl px-3 py-2"
          value={form.description} onChange={(e) => set('description', e.target.value)} />
      </Field>

      <fieldset>
        <legend className="text-sm font-bold text-fg-2">Nişan</legend>
        <div className="mt-2 flex flex-wrap gap-2">
          {icons.map((icon) => (
            <label key={icon}
              className={`grid size-12 cursor-pointer place-items-center rounded-2xl ring-1 ${form.iconKey === icon ? 'bg-brand text-white ring-brand' : 'bg-white/[0.04] text-fg-2 ring-white/10'}`}>
              <input type="radio" name={`icon-${category.id}`} value={icon} checked={form.iconKey === icon} className="sr-only"
                onChange={() => set('iconKey', icon)} />
              <QuizModeIcon iconKey={icon} className="size-6" />
              <span className="sr-only">{icon}</span>
            </label>
          ))}
        </div>
        {errors.iconKey?.map((message) => (
          <p key={message} role="alert" className="mt-2 text-sm font-semibold text-bad">{message}</p>
        ))}
      </fieldset>

      <label className="flex items-start gap-3 rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10">
        <input type="checkbox" className="mt-1 size-5 accent-brand" checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
        <span>
          <span className="block font-bold text-fg">Aktiv</span>
          <span className="block text-sm text-fg-2">Deaktiv kateqoriya və onun kampaniyaları ana səhifədə görünmür.</span>
        </span>
      </label>
      {errors.isActive?.map((message) => (
        <p key={message} role="alert" className="-mt-3 text-sm font-semibold text-bad">{message}</p>
      ))}

      <label className="flex items-start gap-3 rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10">
        <input type="checkbox" className="mt-1 size-5 accent-brand" checked={form.isPreview} onChange={(e) => set('isPreview', e.target.checked)} />
        <span>
          <span className="block font-bold text-fg">Test versiya</span>
          <span className="block text-sm text-fg-2">Kartda "TEST VERSİYA" yazılır: bank oynanılır, amma hələ hazır deyil.</span>
        </span>
      </label>

      <div className="flex flex-wrap gap-3">
        <button type="submit" disabled={saving} className="btn btn-primary min-h-12 px-6" data-testid="category-save">
          {saving ? 'Yadda saxlanılır…' : 'Yadda saxla'}
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
