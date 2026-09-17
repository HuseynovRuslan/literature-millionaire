import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import {
  adminFailure,
  campaignErrors,
  campaignInput,
  getCampaigns,
  updateCampaign,
  type AdminCampaign,
  type CampaignStatus,
} from '../../api/admin'
import { useAdmin } from './adminContext'
import { formatDay } from './campaignDates'

const STATUS: Record<CampaignStatus, { label: string; className: string }> = {
  running: { label: 'Davam edir', className: 'bg-ok/15 text-ok ring-ok/40' },
  scheduled: { label: 'Planlaşdırılıb', className: 'bg-brand/20 text-brand-soft ring-brand-soft/40' },
  disabled: { label: 'Deaktiv', className: 'bg-white/[0.06] text-fg-3 ring-white/15' },
  ended: { label: 'Bitib', className: 'bg-white/[0.04] text-fg-3 ring-white/10' },
}

type Load = { kind: 'loading' } | { kind: 'ready'; campaigns: AdminCampaign[] } | { kind: 'error' }

/** Every campaign, running ones first. Opening next month is one button per campaign. */
export default function AdminCampaignsPage() {
  const { onSignedOut } = useAdmin()
  const location = useLocation()
  const saved = (location.state as { saved?: string } | null)?.saved
  const [load, setLoad] = useState<Load>({ kind: 'loading' })
  const [notice, setNotice] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(saved ? { tone: 'ok', text: saved } : null)
  const [busyId, setBusyId] = useState<number | null>(null)

  const fail = useCallback(
    (err: unknown, otherwise: () => void) => {
      const failure = adminFailure(err)
      if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
      else otherwise()
    },
    [onSignedOut],
  )

  useEffect(() => {
    const controller = new AbortController()
    getCampaigns(controller.signal)
      .then((campaigns) => setLoad({ kind: 'ready', campaigns }))
      .catch((err) => {
        if (!controller.signal.aborted) fail(err, () => setLoad({ kind: 'error' }))
      })
    return () => controller.abort()
  }, [fail])

  async function toggle(campaign: AdminCampaign) {
    const enabling = !campaign.isEnabled
    if (!enabling && campaign.status === 'running'
      && !window.confirm(`"${campaign.quizModeTitle}" kampaniyası indi davam edir. Deaktiv etsəniz, oyunçular onu görməyəcək. Davam edilsin?`)) {
      return
    }

    setBusyId(campaign.id)
    setNotice(null)
    try {
      const updated = await updateCampaign(campaign.id, { ...campaignInput(campaign), isEnabled: enabling })
      setLoad((current) => current.kind === 'ready'
        ? { kind: 'ready', campaigns: current.campaigns.map((c) => (c.id === updated.id ? updated : c)) }
        : current)
      setNotice({ tone: 'ok', text: `#${updated.id} ${enabling ? 'aktiv edildi' : 'deaktiv edildi'}.` })
    } catch (err) {
      const errors = campaignErrors(err)
      fail(err, () => setNotice({
        tone: 'bad',
        text: errors ? Object.values(errors).flat().join(' ') : 'Dəyişiklik yadda saxlanmadı. Yenidən cəhd edin.',
      }))
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="campaigns-title">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="campaigns-title" className="font-display text-2xl font-bold">Kampaniyalar</h1>
          <p className="mt-1 max-w-[60ch] text-sm text-fg-2">
            Oyunçular yalnız davam edən aktiv kampaniyanı görür. Hər kampaniyada hər nömrənin bir cəhdi var: yeni dövr üçün yeni kampaniya açın.
          </p>
        </div>
        <Link to="/admin/campaigns/new" className="btn btn-primary min-h-11 px-5 text-sm">Yeni kampaniya</Link>
      </div>

      {notice && (
        <p role={notice.tone === 'bad' ? 'alert' : 'status'} data-testid="campaigns-notice"
          className={`mt-5 rounded-2xl px-4 py-3 text-sm font-semibold ring-1 ${notice.tone === 'bad' ? 'bg-bad/10 text-fg ring-bad/40' : 'bg-ok/10 text-fg ring-ok/30'}`}>
          {notice.text}
        </p>
      )}

      {load.kind === 'loading' && <p role="status" className="mt-6 text-fg-2">Yüklənir…</p>}
      {load.kind === 'error' && <p role="alert" className="mt-6 font-semibold text-fg">Kampaniyaları yükləmək alınmadı.</p>}
      {load.kind === 'ready' && load.campaigns.length === 0 && <p className="mt-6 text-fg-2">Hələ kampaniya yoxdur.</p>}

      {load.kind === 'ready' && load.campaigns.length > 0 && (
        <ul className="mt-5 flex flex-col gap-3" data-testid="campaign-list">
          {load.campaigns.map((c) => (
            <li key={c.id} data-testid={`campaign-${c.id}`}
              className={`rounded-2xl px-4 py-4 ring-1 ${c.status === 'ended' ? 'bg-white/[0.02] ring-white/[0.06]' : 'bg-white/[0.04] ring-white/10'}`}>
              <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
                <div className="min-w-0">
                  <p className="flex flex-wrap items-center gap-2">
                    <span className="font-display text-lg font-bold text-fg">{c.quizModeTitle}</span>
                    <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ring-1 ${STATUS[c.status].className}`}>{STATUS[c.status].label}</span>
                    <span className="text-xs font-semibold text-fg-3">#{c.id}</span>
                  </p>
                  {c.bookTitle && c.bookTitle !== c.quizModeTitle && <p className="text-sm text-fg-2">Kitab: {c.bookTitle}</p>}
                </div>
                <p className="font-semibold tabular-nums text-fg">{formatDay(c.startDate)} – {formatDay(c.endDate)}</p>
              </div>

              <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-1 text-sm sm:grid-cols-4">
                <Fact term="Keçid balı" value={`${c.passingScore} / 10`} />
                <Fact term="Şəkilli sual" value={String(c.imageQuestionsPerQuiz)} />
                <Fact term="Cəhdlər" value={`${c.attemptsStarted} başladı · ${c.attemptsCompleted} bitirdi`} />
                <Fact term="Mükafat" value={c.rewardTitle} />
              </dl>

              {c.issues.length > 0 && (
                <ul className="mt-3 flex flex-col gap-1 rounded-xl bg-bad/10 px-3 py-2 text-sm text-fg ring-1 ring-bad/30" data-testid="campaign-issues">
                  {c.issues.map((issue) => <li key={issue}>⚠ {issue}</li>)}
                </ul>
              )}

              <div className="mt-3 flex flex-wrap gap-2">
                <Link to={`/admin/campaigns/${c.id}`} className="btn btn-secondary min-h-10 px-4 text-sm">Düzəlt</Link>
                <Link to={`/admin/campaigns/new?from=${c.id}`} className="btn btn-secondary min-h-10 px-4 text-sm">Növbəti dövr</Link>
                {c.status !== 'ended' && (
                  <button type="button" onClick={() => void toggle(c)} disabled={busyId !== null} className="btn btn-secondary min-h-10 px-4 text-sm">
                    {c.isEnabled ? 'Deaktiv et' : 'Aktiv et'}
                  </button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function Fact({ term, value }: { term: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-bold uppercase tracking-wider text-fg-3">{term}</dt>
      <dd className="font-semibold break-words text-fg">{value}</dd>
    </div>
  )
}
