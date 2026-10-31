import QrCode from './QrCode'
import type { QrLoginState } from '../hooks/useQrLogin'

/**
 * Where QRLog should send the person back to once they have approved: the very screen they left.
 * Without it they land on the site's front door - the admin who started in the panel ends up in the game.
 * QRLog only accepts a return address on this site, so this is a hint, not a redirect anyone can dictate.
 */
function withReturnAddress(appConfirmUrl: string): string {
  const separator = appConfirmUrl.includes('?') ? '&' : '?'
  return `${appConfirmUrl}${separator}return=${encodeURIComponent(window.location.href)}`
}

/**
 * The QRLog sign-in QR: the way in for players on the registration screen and for administrators on the
 * admin panel's sign-in screen. Most people reaching either are colleagues already carrying QRLog, so the QR
 * is the screen rather than something behind a button.
 *
 * Only the code is in the QR. The browser keeps a separate secret and polls with that, so the QR
 * being visible to the room gives nothing away - see the backend's IQrLoginService.
 */
export default function QrLoginPanel({ state, onRetry, hint = 'Telefonunuzda QRLog tətbiqini açın və kodu skan edin — adınız və nömrəniz özü gələcək.' }: {
  state: QrLoginState
  /**
   * "Yeni QR kod". It must start a genuinely new sign-in - begin({ fresh: true }) - never resume the one the
   * person is standing here trying to get away from, which is what made this button look broken.
   */
  onRetry: () => void
  /** The line under the heading: what scanning will do on this particular screen. */
  hint?: string
}) {
  return (
    <div className="flex min-w-0 flex-col items-center text-center" data-testid="qrlog-panel" aria-live="polite">
      {/* The logo and the code share one white card: the QR needs a light background to scan, and on a
          dark studio screen a floating white square would read as a hole rather than as a sign-in. */}
      <div className="flex w-full max-w-[22rem] flex-col items-center gap-3 rounded-3xl bg-white p-[clamp(0.9rem,1.6vw,1.4rem)] shadow-[0_1rem_2.4rem_-0.8rem_rgba(0,0,0,0.55)]">
        <img
          src="/brand/qrlog-logo.webp"
          alt="QRLog"
          width={720}
          height={265}
          className="h-[clamp(1.6rem,2.4vw,2.2rem)] w-auto"
          data-testid="qrlog-logo"
        />

        {state.kind === 'waiting' ? (
          <QrCode
            value={state.qrValue}
            title="QRLog tətbiqi ilə oxutmaq üçün QR kod"
            className="aspect-square w-full max-w-[16rem]"
          />
        ) : (
          // Same square either way, so the card does not jump while a code is being minted or renewed.
          <div className="grid aspect-square w-full max-w-[16rem] place-items-center rounded-xl bg-ink-950/5 px-4 text-center">
            {state.kind === 'starting' || state.kind === 'idle' ? (
              <p className="font-semibold text-ink-950/60">QR kod hazırlanır…</p>
            ) : state.kind === 'confirmed' ? (
              // QRLog has said yes and the screen is a moment from moving on. This used to fall through to the
              // error copy below, so the last thing a person saw after a successful approval was "Əlaqə alınmadı".
              <div data-testid="qrlog-confirmed">
                <p className="font-display text-[1.6rem] font-extrabold leading-none text-ok">✓</p>
                <p lang="az" className="mt-2 font-bold text-ink-950/75">Təsdiqləndi</p>
                <p lang="az" className="mt-1 text-sm font-semibold text-ink-950/55">{state.fullName}</p>
              </div>
            ) : (
              <div>
                <p lang="az" className="font-bold text-ink-950/75">
                  {state.kind === 'expired' ? 'QR kodun vaxtı bitdi' : 'Əlaqə alınmadı'}
                </p>
                <button type="button" onClick={onRetry} className="tap mt-3 rounded-xl bg-brand px-5 py-2.5 font-display font-bold text-white" data-testid="qrlog-retry">
                  Yeni QR kod
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      <p lang="az" className="mt-4 font-display text-[clamp(1.1rem,1.5vw,1.4rem)] font-extrabold">
        QRLog tətbiqi ilə oxudun
      </p>
      <p lang="az" className="mt-1 max-w-[34ch] text-[clamp(0.9rem,1.05vw,1rem)] font-medium leading-snug text-fg-2">
        {hint}
      </p>
      {state.kind === 'waiting' && (
        <p className="mt-2 text-[clamp(0.8rem,0.9vw,0.88rem)] font-bold tabular-nums text-fg-3" data-testid="qrlog-countdown">
          Kodun vaxtı: {state.secondsLeft} saniyə
        </p>
      )}

      {/* Already on the phone the QRLog app is on? Then there is nothing to scan - a phone cannot photograph
          its own screen - so QRLog is opened to approve this same code instead. It opens in its own tab so this
          one keeps waiting, and the sign-in finishes here by itself the moment QRLog confirms. */}
      {state.kind === 'waiting' && state.appConfirmUrl && (
        <div className="mt-5 w-full max-w-[22rem] border-t border-white/10 pt-4">
          <a
            href={withReturnAddress(state.appConfirmUrl)}
            target="_blank"
            rel="noopener noreferrer"
            className="btn btn-primary min-h-12 w-full px-5"
            data-testid="qrlog-app-confirm"
          >
            QRLog tətbiqi ilə təsdiqlə
          </a>
          <p lang="az" className="mt-2 text-[clamp(0.8rem,0.95vw,0.9rem)] font-medium leading-snug text-fg-3">
            Telefondan girirsinizsə, bu düyməni basın: QRLog açılacaq, təsdiqləyəcəksiniz və bu səhifə özü davam edəcək.
          </p>
        </div>
      )}

    </div>
  )
}
