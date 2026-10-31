import { useNavigate } from 'react-router-dom'
import GameShowShell from '../components/home/GameShowShell'
import { PRIMARY_CTA } from '../components/home/gameShowClasses'

/**
 * Where an ordinary phone camera lands when it scans the kiosk's sign-in QR.
 *
 * The QR is meant for the QRLog app, which is signed in as the employee and can vouch for them. A
 * browser reaching this URL cannot prove who anyone is, so the page says so plainly rather than
 * leaving someone staring at a page that does nothing. It deliberately shows no part of the sign-in:
 * the code in the address bar is useless without the secret the kiosk kept.
 */
export default function QrLoginHelpPage() {
  const navigate = useNavigate()

  return (
    <GameShowShell>
      <section
        className="card rise mx-auto flex w-full max-w-[44rem] flex-col items-center px-[clamp(1.4rem,3vw,2.6rem)] py-[clamp(1.6rem,3vh,2.4rem)] text-center"
        data-testid="qr-help"
      >
        <h1 lang="az" className="font-display text-[clamp(1.6rem,3vw,2.4rem)] font-extrabold leading-tight">
          Bu kodu QRLog tətbiqi ilə oxudun
        </h1>
        <p lang="az" className="mt-3 max-w-[46ch] text-[clamp(1rem,1.25vw,1.15rem)] font-medium leading-snug text-fg-2">
          Siz bu QR kodu telefonun adi kamerası ilə oxudunuz. Adınızı və nömrənizi avtomatik doldurmaq üçün
          onu <strong className="font-bold text-fg">QRLog tətbiqindən</strong> skan etmək lazımdır — çünki kim
          olduğunuzu yalnız QRLog təsdiqləyə bilər.
        </p>
        <p lang="az" className="mt-3 max-w-[46ch] text-[clamp(0.9rem,1.1vw,1rem)] font-medium text-fg-3">
          QRLog tətbiqiniz yoxdursa, ekranda məlumatlarınızı əl ilə də yaza bilərsiniz.
        </p>
        <button type="button" onClick={() => navigate('/')} className={`${PRIMARY_CTA} mt-6 px-10`} data-testid="qr-help-home">
          Ana səhifə
        </button>
      </section>
    </GameShowShell>
  )
}
