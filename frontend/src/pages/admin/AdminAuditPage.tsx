import { useEffect, useState } from 'react'
import { adminFailure, getAuditLog, type AuditEntry } from '../../api/admin'
import { useAdmin } from './adminContext'

/** What each recorded action means, in words. Unknown actions (from later phases) show as they are stored. */
const ACTION_LABELS: Record<string, string> = {
  'sign-in': 'Daxil oldu (QRLog)',
  'sign-in-link': 'Daxil oldu (təcili link)',
  'sign-in-denied': 'Giriş rədd edildi',
  'sign-in-link-denied': 'Təcili link rədd edildi',
  'sign-out': 'Çıxış etdi',
  'campaign-created': 'Kampaniya yaratdı',
  'campaign-updated': 'Kampaniyanı dəyişdi',
  'results-exported': 'Nəticələri ixrac etdi',
  'attempt-reset': 'Cəhdi sıfırladı',
  'participant-removed': 'İştirakçını sildi',
  'image-uploaded': 'Şəkil yüklədi',
  'book-created': 'Kitab əlavə etdi',
  'book-updated': 'Kitabı dəyişdi',
}

const ENTITY_LABELS: Record<string, string> = {
  campaign: 'Kampaniya',
  participant: 'İştirakçı',
  image: 'Şəkil',
  book: 'Kitab',
}

const TIME =new Intl.DateTimeFormat('az-Latn-AZ', {
  timeZone: 'Asia/Baku',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
})

type Load = { kind: 'loading' } | { kind: 'ready'; entries: AuditEntry[] } | { kind: 'error' }

/** The audit trail, newest first. Read-only: nothing in the panel can change it. */
export default function AdminAuditPage() {
  const { onSignedOut } = useAdmin()
  const [load, setLoad] = useState<Load>({ kind: 'loading' })

  useEffect(() => {
    const controller = new AbortController()
    getAuditLog(controller.signal)
      .then((entries) => setLoad({ kind: 'ready', entries }))
      .catch((err) => {
        if (controller.signal.aborted) return
        const failure = adminFailure(err)
        if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
        else setLoad({ kind: 'error' })
      })
    return () => controller.abort()
  }, [onSignedOut])

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="audit-title">
      <h1 id="audit-title" className="font-display text-2xl font-bold">Jurnal</h1>
      <p className="mt-1 text-sm text-fg-2">Panelə girişlər və dəyişikliklər, ən yenisi yuxarıda. Qeydlər dəyişdirilə və silinə bilməz.</p>

      {load.kind === 'loading' && <p role="status" className="mt-6 text-fg-2">Yüklənir…</p>}
      {load.kind === 'error' && <p role="alert" className="mt-6 font-semibold text-fg">Jurnalı yükləmək alınmadı.</p>}
      {load.kind === 'ready' && load.entries.length === 0 && <p className="mt-6 text-fg-2">Hələ qeyd yoxdur.</p>}
      {load.kind === 'ready' && load.entries.length > 0 && (
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[40rem] text-left text-sm" data-testid="audit-table">
            <thead>
              <tr className="border-b border-white/10 text-xs uppercase tracking-wider text-fg-3">
                <th scope="col" className="py-2 pr-4 font-bold">Vaxt</th>
                <th scope="col" className="py-2 pr-4 font-bold">Kim</th>
                <th scope="col" className="py-2 pr-4 font-bold">Əməliyyat</th>
                <th scope="col" className="py-2 font-bold">Qeyd</th>
              </tr>
            </thead>
            <tbody>
              {load.entries.map((entry) => (
                <tr key={entry.id} className="border-b border-white/[0.06] align-top">
                  <td className="py-2.5 pr-4 tabular-nums text-fg-2">{TIME.format(new Date(entry.atUtc))}</td>
                  <td className="py-2.5 pr-4">
                    <span className="font-semibold text-fg">{entry.actorName}</span>
                    <span className="block text-xs tabular-nums text-fg-3">{entry.actorPhone}</span>
                  </td>
                  <td className={`py-2.5 pr-4 font-semibold ${entry.action.endsWith('-denied') ? 'text-bad' : 'text-fg'}`}>
                    {ACTION_LABELS[entry.action] ?? entry.action}
                  </td>
                  <td className="py-2.5 text-fg-2">
                    {[entry.entityType && (ENTITY_LABELS[entry.entityType] ?? entry.entityType), entry.entityId].filter(Boolean).join(' #')}
                    {entry.details && <span className="block">{entry.details}</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
