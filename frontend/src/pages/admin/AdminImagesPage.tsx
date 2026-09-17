import { useCallback, useEffect, useRef, useState } from 'react'
import {
  adminFailure,
  getUploads,
  refusalMessage,
  uploadImage,
  UPLOAD_ACCEPT,
  UPLOAD_MAX_BYTES,
  type ImageKind,
  type UploadedImage,
} from '../../api/admin'
import { useAdmin } from './adminContext'

const KINDS: { value: ImageKind; label: string; hint: string }[] = [
  { value: 'question', label: 'Sual şəkli', hint: 'Oyunda sualın üstündə göstərilir.' },
  { value: 'cover', label: 'Kitab üz qabığı', hint: 'Ayın kitabı kartında göstərilir.' },
]

function kilobytes(bytes: number): string {
  return `${Math.max(1, Math.round(bytes / 1024))} KB`
}

/**
 * /admin/images: the picture library. Everything sent here is decoded and written out again as WebP by the
 * server, so what is stored is only the picture - no camera metadata, nothing hidden in the file - and the
 * address it gets never changes, because the file is named after its own content.
 */
export default function AdminImagesPage() {
  const { onSignedOut } = useAdmin()
  const [kind, setKind] = useState<ImageKind>('question')
  const [images, setImages] = useState<UploadedImage[] | null>(null)
  const [failed, setFailed] = useState(false)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null)
  const [dragging, setDragging] = useState(false)
  const [copied, setCopied] = useState<string | null>(null)
  const fileInput = useRef<HTMLInputElement>(null)

  const fail = useCallback((err: unknown, otherwise: () => void) => {
    const failure = adminFailure(err)
    if (failure === 'signed-out' || failure === 'not-an-admin') onSignedOut()
    else otherwise()
  }, [onSignedOut])

  const reload = useCallback((signal?: AbortSignal) => {
    getUploads(kind, signal)
      .then((list) => { setImages(list); setFailed(false) })
      .catch((err) => { if (!signal?.aborted) fail(err, () => setFailed(true)) })
  }, [kind, fail])

  useEffect(() => {
    const controller = new AbortController()
    reload(controller.signal)
    return () => controller.abort()
  }, [reload])

  async function send(files: FileList | null) {
    const file = files?.[0]
    if (!file) return
    if (file.size > UPLOAD_MAX_BYTES) {
      setNotice({ tone: 'bad', text: `Fayl ${UPLOAD_MAX_BYTES / (1024 * 1024)} MB-dan böyükdür. Kiçik şəkil seçin.` })
      return
    }

    setBusy(true)
    setNotice(null)
    try {
      const image = await uploadImage(file, kind)
      setNotice({
        tone: 'ok',
        text: image.alreadyExisted
          ? `Bu şəkil artıq yüklənib: ${image.url}`
          : `Yükləndi: ${image.width}×${image.height}, ${kilobytes(image.bytes)}.`,
      })
      setImages((current) => (current && current.some((i) => i.id === image.id) ? current : [image, ...(current ?? [])]))
    } catch (err) {
      fail(err, () => setNotice({ tone: 'bad', text: refusalMessage(err) ?? 'Şəkli yükləmək alınmadı. Yenidən cəhd edin.' }))
    } finally {
      setBusy(false)
      if (fileInput.current) fileInput.current.value = ''
    }
  }

  async function copy(url: string) {
    try {
      await navigator.clipboard.writeText(url)
      setCopied(url)
    } catch {
      // Clipboard access can be refused (an old browser, a page not served over https): the address is
      // selectable on screen, so there is still a way to take it.
      setCopied(null)
      setNotice({ tone: 'bad', text: 'Kopyalamaq alınmadı. Ünvanı seçib əl ilə köçürün.' })
    }
  }

  return (
    <section className="card rounded-3xl px-6 py-6 max-sm:px-3" aria-labelledby="images-title">
      <h1 id="images-title" className="font-display text-2xl font-bold">Şəkillər</h1>
      <p className="mt-1 max-w-[70ch] text-sm text-fg-2">
        JPG, PNG və ya WebP göndərin (ən çoxu {UPLOAD_MAX_BYTES / (1024 * 1024)} MB). Server şəkli özü kiçildir və WebP formatına çevirir,
        beləcə sayt tez açılır və faylın içindəki əlavə məlumat (məsələn, telefonun yer koordinatı) saxlanmır.
        Eyni şəkli iki dəfə göndərsəniz, bir dəfə saxlanılır.
      </p>

      <fieldset className="mt-5">
        <legend className="text-sm font-bold text-fg-2">Növ</legend>
        <div className="mt-2 flex flex-wrap gap-2">
          {KINDS.map((option) => (
            <label key={option.value}
              className={`flex cursor-pointer items-center gap-2 rounded-xl px-4 py-2.5 text-sm font-bold ring-1 ${kind === option.value ? 'bg-brand text-white ring-brand' : 'bg-white/[0.04] text-fg-2 ring-white/10'}`}>
              <input type="radio" name="kind" value={option.value} checked={kind === option.value} className="sr-only"
                onChange={() => { setKind(option.value); setImages(null); setNotice(null) }} />
              {option.label}
            </label>
          ))}
        </div>
        <p className="mt-2 text-xs text-fg-3">{KINDS.find((k) => k.value === kind)?.hint}</p>
      </fieldset>

      <div
        data-testid="upload-drop"
        onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => { e.preventDefault(); setDragging(false); void send(e.dataTransfer.files) }}
        className={`mt-5 rounded-2xl border-2 border-dashed px-6 py-8 text-center ${dragging ? 'border-brand-soft bg-brand/10' : 'border-white/15 bg-white/[0.03]'}`}
      >
        <p className="font-semibold text-fg-2">Şəkli bura sürüşdürün</p>
        <p className="mt-1 text-sm text-fg-3">və ya</p>
        <input ref={fileInput} id="upload-file" type="file" accept={UPLOAD_ACCEPT} className="sr-only"
          onChange={(e) => void send(e.target.files)} />
        <label htmlFor="upload-file" className="btn btn-primary mt-3 inline-flex min-h-11 cursor-pointer px-5 text-sm" aria-disabled={busy}>
          {busy ? 'Yüklənir…' : 'Fayl seçin'}
        </label>
      </div>

      {notice && (
        <p role={notice.tone === 'bad' ? 'alert' : 'status'} data-testid="upload-notice"
          className={`mt-4 rounded-2xl px-4 py-3 text-sm font-semibold ring-1 ${notice.tone === 'bad' ? 'bg-bad/10 text-fg ring-bad/40' : 'bg-ok/10 text-fg ring-ok/30'}`}>
          {notice.text}
        </p>
      )}

      {failed && <p role="alert" className="mt-6 font-semibold text-fg">Şəkilləri yükləmək alınmadı.</p>}
      {!images && !failed && <p role="status" className="mt-6 text-fg-2">Yüklənir…</p>}
      {images?.length === 0 && <p className="mt-6 text-fg-2">Bu növdə hələ şəkil yoxdur.</p>}

      {images && images.length > 0 && (
        <ul className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3" data-testid="image-list">
          {images.map((image) => (
            <li key={image.id} className="flex flex-col overflow-hidden rounded-2xl bg-white/[0.04] ring-1 ring-white/10" data-testid={`image-${image.id}`}>
              <img src={image.url} alt={image.originalFileName} loading="lazy" width={image.width} height={image.height}
                className="aspect-[4/3] w-full bg-ink-950 object-contain" />
              <div className="flex flex-1 flex-col gap-2 px-3 py-3">
                <p className="truncate text-sm font-bold text-fg" title={image.originalFileName}>{image.originalFileName}</p>
                <p className="text-xs tabular-nums text-fg-3">{image.width}×{image.height} · {kilobytes(image.bytes)}</p>
                <input readOnly value={image.url} onFocus={(e) => e.target.select()} aria-label="Şəklin ünvanı"
                  className="field min-h-9 w-full rounded-lg px-2 text-xs" />
                <button type="button" onClick={() => void copy(image.url)} className="btn btn-secondary min-h-9 px-3 text-xs">
                  {copied === image.url ? 'Kopyalandı' : 'Ünvanı kopyala'}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
