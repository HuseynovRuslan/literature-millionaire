import { useCallback, useEffect, useState } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router-dom'
import {
  adminFailure,
  getCampaignResults,
  getCampaigns,
  refusalMessage,
  removeParticipant,
  resetAttempt,
  resultsExportUrl,
  type AdminCampaign,
  type AttemptRow,
  type CampaignResults,
} from '../../api/admin'
import { useAdmin } from './adminContext'
import { formatDay } from './campaignDates'

const TIME_PARTS = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Baku',
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

/** "17.09 12:10", Baku time. Built from parts: browsers print "az" dates without a year as "09-17". */
function bakuTime(iso: string): string {
  const part = (type: string) => TIME_PARTS.formatToParts(new Date(iso)).find((p) => p.type === type)?.value ?? ''
  return `${part('day')}.${part('month')} ${part('hour')}:${part('minute')}`
}

function duration(seconds: number | null): string {
  if (seconds === null) return '—'
  const whole = Math.round(seconds)
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`
}

/** A correction being confirmed: which row, which kind. */
type Pending = { kind: 'reset' | 'remove'; row: AttemptRow }

/**
 * /admin/results/<campaignId>: who played a campaign, ranked the way the public leaderboard ranks them; the Excel
 * list prizes are handed out from; and the two corrections that delete data. /admin/results picks a campaign.
 */
export default function AdminResultsPage() {
  const { onSignedOut } = useAdmin()
  const navigate = useNavigate()
  const { campaignId: param } = useParams()
  const campaignId = param ? Number(param) : null

  const [campaigns, setCampaigns] = useState<AdminCampaign[] | null>(null)
  const [results, setResults] = useState<CampaignResults | null>(null)
  const [failed, setFailed] = useState(false)
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  // Bumped after a correction so the list is read again from the server.
  const [version, setVersion] = useState(0)
  const [pending, setPending] = useState<Pending | null>(null)
  const [notice, setNotice] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null)

  const fail = useCallback((err: unknown) => {
    const failure = adminFailure(err)
    if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
    else setFailed(true)
  }, [onSignedOut])

  useEffect(() => {
    const controller = new AbortController()
    getCampaigns(controller.signal)
      .then(setCampaigns)
      .catch((err) => { if (!controller.signal.aborted) fail(err) })
    return () => controller.abort()
  }, [fail])

  // Search as the admin types, once they pause.
  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim()), 300)
    return () => window.clearTimeout(timer)
  }, [search])

  useEffect(() => {
    if (campaignId === null) return
    const controller = new AbortController()
    getCampaignResults(campaignId, query, controller.signal)
      .then((data) => { setResults(data); setFailed(false) })
      .catch((err) => { if (!controller.signal.aborted) fail(err) })
    return () => controller.abort()
  }, [campaignId, query, version, fail])

  if (campaignId === null && campaigns) {
    const first = campaigns.find((c) => c.status === 'running') ?? campaigns[0]
    return first ? <Navigate to={`/admin/results/${first.id}`} replace /> : (
      <section className="card rounded-3xl px-6 py-6"><h1 className="font-display text-2xl font-bold">Nəticələr</h1><p className="mt-4 text-fg-2">Hələ kampaniya yoxdur.</p></section>
    )
  }

  const shown = results && results.campaign.id === campaignId ? results : null

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="results-title">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <h1 id="results-title" className="font-display text-2xl font-bold">Nəticələr</h1>
          <label htmlFor="results-campaign" className="sr-only">Kampaniya</label>
          <select id="results-campaign" className="field mt-3 min-h-11 max-w-full rounded-xl px-3" value={campaignId ?? ''}
            onChange={(e) => { setPending(null); setNotice(null); navigate(`/admin/results/${e.target.value}`) }}>
            {(campaigns ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                #{c.id} {c.quizModeTitle} · {formatDay(c.startDate)} – {formatDay(c.endDate)}
              </option>
            ))}
          </select>
        </div>
        {campaignId !== null && (
          <a href={resultsExportUrl(campaignId)} download className="btn btn-primary min-h-11 px-5 text-sm" data-testid="results-export">
            Excel-ə ixrac
          </a>
        )}
      </div>

      {failed && <p role="alert" className="mt-6 font-semibold text-fg">Nəticələri yükləmək alınmadı.</p>}
      {!shown && !failed && <p role="status" className="mt-6 text-fg-2">Yüklənir…</p>}

      {shown && (
        <>
          <p className="mt-3 text-sm text-fg-2">
            Keçid balı {shown.campaign.passingScore}/10 · Mükafat: {shown.campaign.rewardTitle}. Excel faylında tam telefon nömrələri olur, ixrac Jurnala yazılır.
          </p>

          <dl className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4" data-testid="results-totals">
            <Total term="Başladı" value={shown.started} />
            <Total term="Bitirdi" value={shown.completed} />
            <Total term="Keçdi" value={shown.passed} />
            <Total term="Bitməyib" value={shown.unfinished} />
          </dl>

          <div className="mt-5">
            <label htmlFor="results-search" className="block text-sm font-bold text-fg-2">Axtarış</label>
            <input id="results-search" type="search" className="field mt-1.5 min-h-11 w-full max-w-[28rem] rounded-xl px-3"
              placeholder="Ad və ya nömrənin rəqəmləri" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>

          {notice && (
            <p role={notice.tone === 'bad' ? 'alert' : 'status'} data-testid="results-notice"
              className={`mt-4 rounded-2xl px-4 py-3 text-sm font-semibold ring-1 ${notice.tone === 'bad' ? 'bg-bad/10 text-fg ring-bad/40' : 'bg-ok/10 text-fg ring-ok/30'}`}>
              {notice.text}
            </p>
          )}

          {shown.attempts.length === 0 ? (
            <p className="mt-6 text-fg-2">{query ? 'Axtarışa uyğun iştirakçı yoxdur.' : 'Bu kampaniyada hələ heç kim oynamayıb.'}</p>
          ) : (
            <ul className="mt-4 flex flex-col gap-2" data-testid="results-list">
              {shown.attempts.map((row) => (
                <li key={row.attemptId} data-testid={`attempt-${row.attemptId}`} className="rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10">
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
                    <span className={`w-10 shrink-0 font-display text-lg font-bold tabular-nums ${row.rank !== null && row.rank <= 3 ? 'text-sun' : 'text-fg-2'}`}>
                      {row.rank ?? '—'}
                    </span>
                    <div className="min-w-[10rem] flex-1">
                      <p className="font-bold break-words text-fg">{row.fullName}</p>
                      <p className="text-xs tabular-nums text-fg-3">{row.phone} · başladı {bakuTime(row.startedAtUtc)}</p>
                    </div>
                    {row.completedAtUtc ? (
                      <p className="text-sm tabular-nums text-fg">
                        <span className="font-bold">{row.correctAnswers}/{row.totalQuestions}</span> düzgün ·{' '}
                        <span className="font-bold">{row.pointsEarned}</span> xal · {duration(row.durationSeconds)}
                        {row.passed ? <span className="ml-2 rounded-full bg-ok/15 px-2 py-0.5 text-xs font-bold text-ok ring-1 ring-ok/40">Keçdi</span> : null}
                      </p>
                    ) : (
                      <p className="text-sm font-semibold text-fg-3">Bitməyib</p>
                    )}
                    <div className="flex gap-2">
                      <button type="button" className="btn btn-secondary min-h-10 px-3 text-xs" onClick={() => { setNotice(null); setPending({ kind: 'reset', row }) }}>
                        Cəhdi sıfırla
                      </button>
                      <button type="button" className="btn btn-secondary min-h-10 px-3 text-xs" onClick={() => { setNotice(null); setPending({ kind: 'remove', row }) }}>
                        İştirakçını sil
                      </button>
                    </div>
                  </div>

                  {pending?.row.attemptId === row.attemptId && (
                    <Confirm
                      key={pending.kind}
                      pending={pending}
                      onCancel={() => setPending(null)}
                      onDone={(text) => { setPending(null); setNotice({ tone: 'ok', text }); setVersion((v) => v + 1) }}
                      onRefused={(text) => setNotice({ tone: 'bad', text })}
                      onSignedOut={onSignedOut}
                    />
                  )}
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}

function Total({ term, value }: { term: string; value: number }) {
  return (
    <div className="rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10">
      <dt className="text-xs font-bold uppercase tracking-wider text-fg-3">{term}</dt>
      <dd className="font-display text-2xl font-bold tabular-nums text-fg">{value}</dd>
    </div>
  )
}

/** Says exactly what will be deleted, asks why, and only then deletes. */
function Confirm({ pending, onCancel, onDone, onRefused, onSignedOut }: {
  pending: Pending
  onCancel: () => void
  onDone: (text: string) => void
  onRefused: (text: string) => void
  onSignedOut: () => void
}) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const { row, kind } = pending
  const result = row.completedAtUtc ? `${row.correctAnswers}/${row.totalQuestions} düzgün, ${row.pointsEarned} xal` : 'bitməmiş'

  async function confirm() {
    setBusy(true)
    try {
      if (kind === 'reset') {
        await resetAttempt(row.attemptId, reason)
        onDone(`${row.fullName}: cəhd sıfırlandı. Bu kampaniyanı yenidən oynaya bilər.`)
      } else {
        await removeParticipant(row.participantId, reason)
        onDone(`${row.fullName} və ${row.participantAttempts} cəhdi silindi.`)
      }
    } catch (err) {
      const failure = adminFailure(err)
      if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
      else onRefused(refusalMessage(err) ?? 'Əməliyyat alınmadı. Yenidən cəhd edin.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mt-3 rounded-xl bg-bad/10 px-4 py-3 ring-1 ring-bad/40" data-testid="results-confirm">
      <p className="text-sm font-semibold text-fg">
        {kind === 'reset'
          ? `${row.fullName} adlı iştirakçının bu kampaniyadakı cəhdi (${result}) silinəcək və o, yenidən oynaya biləcək.`
          : `${row.fullName} və bütün kampaniyalardakı ${row.participantAttempts} cəhdi silinəcək. Bu, test girişləri üçündür.`}
        {' '}Geri qaytarmaq olmaz.
      </p>
      <label htmlFor={`reason-${row.attemptId}`} className="mt-3 block text-sm font-bold text-fg-2">Səbəb (Jurnala yazılır)</label>
      <input id={`reason-${row.attemptId}`} type="text" maxLength={300} autoFocus className="field mt-1.5 min-h-11 w-full rounded-xl px-3"
        placeholder={kind === 'reset' ? 'məsələn: texniki nasazlıq' : 'məsələn: test girişi'} value={reason} onChange={(e) => setReason(e.target.value)} />
      <div className="mt-3 flex flex-wrap gap-2">
        <button type="button" disabled={busy || reason.trim().length < 3} onClick={() => void confirm()}
          className="btn btn-primary min-h-10 px-4 text-sm" data-testid="results-confirm-button">
          {busy ? 'Silinir…' : kind === 'reset' ? 'Cəhdi sıfırla' : 'İştirakçını sil'}
        </button>
        <button type="button" onClick={onCancel} className="btn btn-secondary min-h-10 px-4 text-sm">Ləğv et</button>
      </div>
    </div>
  )
}
