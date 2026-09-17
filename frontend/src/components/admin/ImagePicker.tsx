import { useCallback, useEffect, useState } from 'react'
import { adminFailure, getUploads, refusalMessage, uploadImage, UPLOAD_ACCEPT, UPLOAD_MAX_BYTES, type ImageKind, type UploadedImage } from '../../api/admin'

/**
 * Choosing a picture the site already has: a bank's cover, or a question's illustration. Shows what has been
 * uploaded for that kind, and can upload one more without leaving the form.
 *
 * A stored picture is always one this site serves — the server refuses any other address — so this is
 * deliberately a picker rather than a text field where an address could be pasted.
 */
export default function ImagePicker({ value, onChange, onSignedOut, invalid, kind = 'cover' }: {
  /** The chosen picture's address, or "" for none. */
  value: string
  onChange: (imageUrl: string) => void
  onSignedOut: () => void
  invalid?: boolean
  /** Which library to show and upload into: a bank's cover, or a question's illustration. */
  kind?: ImageKind
}) {
  const [pictures, setPictures] = useState<UploadedImage[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [problem, setProblem] = useState<string | null>(null)

  const fail = useCallback((err: unknown, otherwise: () => void) => {
    const failure = adminFailure(err)
    if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
    else otherwise()
  }, [onSignedOut])

  useEffect(() => {
    const controller = new AbortController()
    getUploads(kind, controller.signal)
      .then(setPictures)
      .catch((err) => { if (!controller.signal.aborted) fail(err, () => setPictures([])) })
    return () => controller.abort()
  }, [fail, kind])

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
      const image = await uploadImage(file, kind)
      setPictures((current) => (current?.some((c) => c.id === image.id) ? current : [image, ...(current ?? [])]))
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
        <div className={`shrink-0 overflow-hidden rounded-xl bg-ink-950 ring-1 ring-white/10 ${kind === 'cover' ? 'h-28 w-20' : 'h-24 w-36'}`}>
          {value
            ? <img src={value} alt="Seçilmiş şəkil" className="h-full w-full object-cover" data-testid="cover-chosen" />
            : <span className="grid h-full w-full place-items-center text-xs text-fg-3">Yoxdur</span>}
        </div>
        <div className="flex flex-wrap gap-2">
          <input id="cover-file" type="file" accept={UPLOAD_ACCEPT} className="sr-only" onChange={(e) => void upload(e.target.files)} />
          <label htmlFor="cover-file" className="btn btn-secondary min-h-10 cursor-pointer px-4 text-sm">
            {busy ? 'Yüklənir…' : 'Yeni şəkil yüklə'}
          </label>
          {value && (
            <button type="button" onClick={() => onChange('')} className="btn btn-secondary min-h-10 px-4 text-sm">
              Şəkli sil
            </button>
          )}
        </div>
      </div>

      {problem && <p role="alert" className="mt-3 text-sm font-semibold text-bad">{problem}</p>}

      {pictures && pictures.length > 0 && (
        <ul className="mt-4 flex flex-wrap gap-2" data-testid="cover-list">
          {pictures.map((picture) => (
            <li key={picture.id}>
              <button
                type="button"
                onClick={() => onChange(picture.url)}
                aria-pressed={value === picture.url}
                title={picture.originalFileName}
                className={`overflow-hidden rounded-lg ring-2 transition ${value === picture.url ? 'ring-brand-soft' : 'ring-white/10 hover:ring-white/30'}`}
              >
                <img src={picture.url} alt={picture.originalFileName} loading="lazy"
                  className={`bg-ink-950 object-cover ${kind === 'cover' ? 'h-20 w-14' : 'h-16 w-24'}`} />
              </button>
            </li>
          ))}
        </ul>
      )}
      {pictures?.length === 0 && <p className="mt-3 text-sm text-fg-3">Hələ şəkil yüklənməyib.</p>}
    </div>
  )
}
