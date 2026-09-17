import { useCallback, useEffect, useState } from 'react'
import { adminFailure, getUploads, refusalMessage, uploadImage, UPLOAD_ACCEPT, UPLOAD_MAX_BYTES, type UploadedImage } from '../../api/admin'

/**
 * Choosing a book cover: the pictures already uploaded, plus a way to add one without leaving the form.
 *
 * A cover is a picture from this site and nothing else — the server refuses any other address — so this is
 * deliberately a picker rather than a text field where an address could be pasted.
 */
export default function CoverPicker({ value, onChange, onSignedOut, invalid }: {
  /** The chosen picture's address, or "" for none. */
  value: string
  onChange: (coverImageUrl: string) => void
  onSignedOut: () => void
  invalid?: boolean
}) {
  const [covers, setCovers] = useState<UploadedImage[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [problem, setProblem] = useState<string | null>(null)

  const fail = useCallback((err: unknown, otherwise: () => void) => {
    const failure = adminFailure(err)
    if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
    else otherwise()
  }, [onSignedOut])

  useEffect(() => {
    const controller = new AbortController()
    getUploads('cover', controller.signal)
      .then(setCovers)
      .catch((err) => { if (!controller.signal.aborted) fail(err, () => setCovers([])) })
    return () => controller.abort()
  }, [fail])

  async function upload(files: FileList | null) {
    const file = files?.[0]
    if (!file) return
    if (file.size > UPLOAD_MAX_BYTES) {
      setProblem(`Fayl ${UPLOAD_MAX_BYTES / (1024 * 1024)} MB-dan böyükdür.`)
      return
    }

    setBusy(true)
    setProblem(null)
    try {
      const image = await uploadImage(file, 'cover')
      setCovers((current) => (current?.some((c) => c.id === image.id) ? current : [image, ...(current ?? [])]))
      onChange(image.url)
    } catch (err) {
      fail(err, () => setProblem(refusalMessage(err) ?? 'Şəkli yükləmək alınmadı.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={`rounded-2xl px-4 py-4 ring-1 ${invalid ? 'bg-bad/10 ring-bad/40' : 'bg-white/[0.04] ring-white/10'}`} data-testid="cover-picker">
      <div className="flex flex-wrap items-center gap-3">
        <div className="h-28 w-20 shrink-0 overflow-hidden rounded-xl bg-ink-950 ring-1 ring-white/10">
          {value
            ? <img src={value} alt="Seçilmiş üz qabığı" className="h-full w-full object-cover" data-testid="cover-chosen" />
            : <span className="grid h-full w-full place-items-center text-xs text-fg-3">Yoxdur</span>}
        </div>
        <div className="flex flex-wrap gap-2">
          <input id="cover-file" type="file" accept={UPLOAD_ACCEPT} className="sr-only" onChange={(e) => void upload(e.target.files)} />
          <label htmlFor="cover-file" className="btn btn-secondary min-h-10 cursor-pointer px-4 text-sm">
            {busy ? 'Yüklənir…' : 'Yeni şəkil yüklə'}
          </label>
          {value && (
            <button type="button" onClick={() => onChange('')} className="btn btn-secondary min-h-10 px-4 text-sm">
              Üz qabığını sil
            </button>
          )}
        </div>
      </div>

      {problem && <p role="alert" className="mt-3 text-sm font-semibold text-bad">{problem}</p>}

      {covers && covers.length > 0 && (
        <ul className="mt-4 flex flex-wrap gap-2" data-testid="cover-list">
          {covers.map((cover) => (
            <li key={cover.id}>
              <button
                type="button"
                onClick={() => onChange(cover.url)}
                aria-pressed={value === cover.url}
                title={cover.originalFileName}
                className={`overflow-hidden rounded-lg ring-2 transition ${value === cover.url ? 'ring-brand-soft' : 'ring-white/10 hover:ring-white/30'}`}
              >
                <img src={cover.url} alt={cover.originalFileName} loading="lazy" className="h-20 w-14 bg-ink-950 object-cover" />
              </button>
            </li>
          ))}
        </ul>
      )}
      {covers?.length === 0 && <p className="mt-3 text-sm text-fg-3">Hələ üz qabığı yüklənməyib.</p>}
    </div>
  )
}
