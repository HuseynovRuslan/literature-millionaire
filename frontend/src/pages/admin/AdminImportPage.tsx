import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  adminFailure,
  analyseImport,
  applyImport,
  getQuestionOptions,
  importTemplateUrl,
  refusalMessage,
  IMPORT_ACCEPT,
  IMPORT_MAX_BYTES,
  type ImportReport,
  type ImportResult,
  type QuestionOptions,
} from '../../api/admin'
import { useAdmin } from './adminContext'

const STATUS: Record<string, { label: string; className: string }> = {
  ready: { label: 'Hazır', className: 'bg-ok/15 text-ok ring-ok/40' },
  duplicate: { label: 'Təkrar', className: 'bg-sun/15 text-sun ring-sun/40' },
  problem: { label: 'Problem', className: 'bg-bad/15 text-bad ring-bad/40' },
}

/**
 * /admin/import: a bank somebody prepared in Excel.
 *
 * Uploading only reports — row by row, by the number Excel shows — and the "Əlavə et" button writes exactly
 * what that report called ready. A file of two hundred rows almost always has a few that cannot be played, and
 * seeing which ones while the file is still open is the whole point of doing it in two steps.
 */
export default function AdminImportPage() {
  const { onSignedOut } = useAdmin()
  const [options, setOptions] = useState<QuestionOptions | null>(null)
  const [bookId, setBookId] = useState<number | null>(null)
  const [quizModeId, setQuizModeId] = useState<number | null>(null)
  const [category, setCategory] = useState('')
  const [report, setReport] = useState<ImportReport | null>(null)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [busy, setBusy] = useState<'none' | 'reading' | 'applying'>('none')
  const [problem, setProblem] = useState<string | null>(null)
  const [onlyProblems, setOnlyProblems] = useState(false)

  const fail = useCallback((err: unknown, otherwise: () => void) => {
    const failure = adminFailure(err)
    if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
    else otherwise()
  }, [onSignedOut])

  useEffect(() => {
    const controller = new AbortController()
    getQuestionOptions(controller.signal)
      .then((data) => {
        setOptions(data)
        setBookId((current) => current ?? data.banks[0]?.id ?? null)
      })
      .catch((err) => { if (!controller.signal.aborted) fail(err, () => setProblem('Məlumatı yükləmək alınmadı.')) })
    return () => controller.abort()
  }, [fail])

  async function read(files: FileList | null) {
    const file = files?.[0]
    if (!file || bookId === null || quizModeId === null) return
    if (file.size > IMPORT_MAX_BYTES) {
      setProblem(`Fayl ${IMPORT_MAX_BYTES / (1024 * 1024)} MB-dan böyükdür.`)
      return
    }

    setBusy('reading')
    setProblem(null)
    setResult(null)
    setReport(null)
    try {
      setReport(await analyseImport(file, bookId, quizModeId, category.trim()))
    } catch (err) {
      fail(err, () => setProblem(refusalMessage(err) ?? 'Faylı oxumaq alınmadı.'))
    } finally {
      setBusy('none')
    }
  }

  async function apply() {
    if (!report) return
    setBusy('applying')
    setProblem(null)
    try {
      setResult(await applyImport(report.token))
      setReport(null)
    } catch (err) {
      fail(err, () => setProblem(refusalMessage(err) ?? 'Əlavə etmək alınmadı.'))
    } finally {
      setBusy('none')
    }
  }

  const rows = report ? (onlyProblems ? report.rows.filter((r) => r.status !== 'ready') : report.rows) : []

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="import-title">
      <h1 id="import-title" className="font-display text-2xl font-bold">Toplu idxal</h1>
      <p className="mt-1 max-w-[70ch] text-sm text-fg-2">
        Excel (.xlsx) və ya JSON faylla bir dəfəyə çoxlu sual əlavə edin. Fayl əvvəlcə yoxlanılır: heç nə yazılmır,
        sətir-sətir nə olacağı göstərilir. Sonra "Əlavə et" düyməsi yalnız hazır sətirləri yazır.
        Şəkilli suallar üçün şəkillər əvvəlcədən <Link to="/admin/images" className="underline decoration-white/30 hover:text-fg">Şəkillər</Link> bölməsinə yüklənməlidir.
      </p>
      <p className="mt-2 text-sm">
        <a href={importTemplateUrl()} download className="font-semibold text-brand-soft underline decoration-brand-soft/40" data-testid="import-template">
          Nümunə faylı yüklə (.xlsx)
        </a>
        <span className="text-fg-3"> — sütunlar: Sual, Variant A–D, Düzgün cavab, Çətinlik, Alt kateqoriya, İzah, Şəkil, Alt mətn, Mənbə, Lisenziya.</span>
      </p>

      {options && (
        <div className="mt-6 grid gap-5 sm:grid-cols-3">
          <div className="flex min-w-0 flex-col gap-1.5">
            <label htmlFor="import-bank" className="text-sm font-bold text-fg-2">Bank</label>
            <select id="import-bank" className="field min-h-12 w-full rounded-xl px-3" value={bookId ?? ''}
              onChange={(e) => { setBookId(e.target.value ? Number(e.target.value) : null); setReport(null) }}>
              <option value="">Seçin</option>
              {options.banks.map((bank) => <option key={bank.id} value={bank.id}>{bank.title}</option>)}
            </select>
          </div>
          <div className="flex min-w-0 flex-col gap-1.5">
            <label htmlFor="import-mode" className="text-sm font-bold text-fg-2">Kateqoriya</label>
            <select id="import-mode" className="field min-h-12 w-full rounded-xl px-3" value={quizModeId ?? ''}
              onChange={(e) => { setQuizModeId(e.target.value ? Number(e.target.value) : null); setReport(null) }}>
              <option value="">Seçin</option>
              {options.quizModes.map((mode) => <option key={mode.id} value={mode.id}>{mode.title}</option>)}
            </select>
          </div>
          <div className="flex min-w-0 flex-col gap-1.5">
            <label htmlFor="import-category" className="text-sm font-bold text-fg-2">Alt kateqoriya</label>
            <input id="import-category" list="import-categories" className="field min-h-12 w-full rounded-xl px-3"
              placeholder="Fayl boş olarsa istifadə olunur" value={category} onChange={(e) => setCategory(e.target.value)} />
            <datalist id="import-categories">
              {options.categories.map((c) => <option key={c} value={c} />)}
            </datalist>
          </div>
        </div>
      )}

      <div className="mt-5 rounded-2xl border-2 border-dashed border-white/15 bg-white/[0.03] px-6 py-8 text-center">
        <input id="import-file" type="file" accept={IMPORT_ACCEPT} className="sr-only" onChange={(e) => void read(e.target.files)} />
        <label htmlFor="import-file"
          className={`btn btn-primary inline-flex min-h-12 px-6 ${bookId === null || quizModeId === null ? 'pointer-events-none opacity-50' : 'cursor-pointer'}`}>
          {busy === 'reading' ? 'Yoxlanılır…' : 'Fayl seçin'}
        </label>
        <p className="mt-3 text-sm text-fg-3">
          {bookId === null || quizModeId === null ? 'Əvvəlcə bankı və kateqoriyanı seçin.' : 'Fayl yalnız yoxlanılır, heç nə yazılmır.'}
        </p>
      </div>

      {problem && (
        <p role="alert" data-testid="import-problem" className="mt-4 rounded-2xl bg-bad/10 px-4 py-3 text-sm font-semibold text-fg ring-1 ring-bad/40">
          {problem}
        </p>
      )}

      {result && (
        <p role="status" data-testid="import-result" className="mt-4 rounded-2xl bg-ok/10 px-4 py-3 text-sm font-semibold text-fg ring-1 ring-ok/30">
          {result.added} sual "{result.bankTitle}" bankına əlavə olundu{result.skipped > 0 ? `, ${result.skipped} sətir buraxıldı` : ''}.{' '}
          <Link to="/admin/questions-editor" className="underline">Suallar bölməsində baxın</Link>.
        </p>
      )}

      {report && (
        <div className="mt-6" data-testid="import-report">
          <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Total term="Sətir" value={report.totalRows} />
            <Total term="Hazır" value={report.ready} tone="ok" />
            <Total term="Təkrar" value={report.duplicates} />
            <Total term="Problem" value={report.problems} tone={report.problems > 0 ? 'bad' : undefined} />
          </dl>

          <p className="mt-3 text-sm text-fg-2">
            <strong className="text-fg">{report.fileName}</strong> → {report.bankTitle} · {report.quizModeTitle}
          </p>

          <div className="mt-4 flex flex-wrap items-center gap-3">
            <button type="button" onClick={() => void apply()} disabled={busy !== 'none' || report.ready === 0}
              className="btn btn-primary min-h-12 px-6" data-testid="import-apply">
              {busy === 'applying' ? 'Əlavə olunur…' : `${report.ready} sualı əlavə et`}
            </button>
            <button type="button" onClick={() => setReport(null)} className="btn btn-secondary min-h-12 px-6">Ləğv et</button>
            <label className="flex items-center gap-2 text-sm font-semibold text-fg-2">
              <input type="checkbox" className="size-4 accent-brand" checked={onlyProblems} onChange={(e) => setOnlyProblems(e.target.checked)} />
              Yalnız problemli sətirlər
            </label>
          </div>

          <ul className="mt-4 flex flex-col gap-2" data-testid="import-rows">
            {rows.map((row) => (
              <li key={row.row} data-testid={`import-row-${row.row}`}
                className="flex flex-wrap items-start gap-3 rounded-xl bg-white/[0.04] px-4 py-2.5 ring-1 ring-white/10">
                <span className="w-12 shrink-0 font-bold tabular-nums text-fg-3">#{row.row}</span>
                <span className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-bold ring-1 ${STATUS[row.status].className}`}>
                  {STATUS[row.status].label}
                </span>
                <div className="min-w-[12rem] flex-1">
                  <p className="text-sm font-semibold text-fg">{row.text || <span className="text-fg-3">(boş)</span>}</p>
                  {row.problems.length > 0 && (
                    <ul className="mt-1 flex flex-col gap-0.5 text-xs text-bad">
                      {row.problems.map((message) => <li key={message}>{message}</li>)}
                    </ul>
                  )}
                </div>
                <span className="text-xs text-fg-3">
                  {row.difficulty ?? '—'}{row.category ? ` · ${row.category}` : ''}{row.hasImage ? ' · şəkilli' : ''}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}

function Total({ term, value, tone }: { term: string; value: number; tone?: 'ok' | 'bad' }) {
  return (
    <div className={`rounded-2xl px-4 py-3 ring-1 ${tone === 'ok' ? 'bg-ok/10 ring-ok/30' : tone === 'bad' ? 'bg-bad/10 ring-bad/40' : 'bg-white/[0.04] ring-white/10'}`}>
      <dt className="text-xs font-bold uppercase tracking-wider text-fg-3">{term}</dt>
      <dd className="font-display text-2xl font-bold tabular-nums text-fg">{value}</dd>
    </div>
  )
}
