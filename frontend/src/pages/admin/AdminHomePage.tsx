import { Link } from 'react-router-dom'
import { useAdmin } from './adminContext'

/**
 * The panel's first page: what each section is for, in the order a month is actually run.
 *
 * It used to list what had not been built yet. Everything on that list exists now, so the page says what the
 * panel does instead - somebody opening it for the first time should not have to click through seven sections
 * to find where a new month starts.
 */
const SECTIONS = [
  {
    to: '/admin/campaigns',
    title: 'Kampaniyalar',
    text: 'Yeni dövr açmaq, tarixləri və keçid balını dəyişmək, kampaniyanı dayandırmaq. Ayın işi buradan başlayır: "Növbəti dövr" düyməsi köhnə kampaniyanın ayarlarını götürüb yeni aya keçirir.',
  },
  {
    to: '/admin/results',
    title: 'Nəticələr',
    text: 'Kim oynadı, kim keçdi. Mükafat üçün Excel ixracı; lazım olsa, bir nəfərin cəhdini sıfırlamaq və ya test girişini silmək.',
  },
  {
    to: '/admin/questions-editor',
    title: 'Suallar',
    text: 'Sualları axtarmaq, düzəltmək, yenisini yazmaq. Forma oyunçunun görəcəyi kartı yan-yana göstərir.',
  },
  {
    to: '/admin/import',
    title: 'Toplu idxal',
    text: 'Excel faylla bir dəfəyə çoxlu sual. Əvvəlcə sətir-sətir hesabat verilir, heç nə yazılmır; sonra yalnız hazır sətirlər əlavə olunur.',
  },
  {
    to: '/admin/books',
    title: 'Sual bankları',
    text: 'Ayın kitabı və kolleksiyalar: ad, müəllif, üz qabığı. Yeni ay üçün əvvəlcə bank, sonra kampaniya.',
  },
  {
    to: '/admin/images',
    title: 'Şəkillər',
    text: 'Sual şəkilləri və üz qabıqları. Server şəkli özü kiçildir və WebP-ə çevirir.',
  },
  {
    to: '/admin/categories',
    title: 'Kateqoriyalar',
    text: 'Ana səhifədəki kartların adı, təsviri, nişanı və sırası; "test versiya" nişanı.',
  },
]

export default function AdminHomePage() {
  const { session } = useAdmin()

  return (
    <div className="flex flex-col gap-6">
      <section className="card rounded-3xl px-6 py-6">
        <h1 className="font-display text-2xl font-bold">Xoş gəldiniz, {session.fullName}</h1>
        <p className="mt-2 max-w-[60ch] text-fg-2">
          Panelə girişlər və buradakı hər dəyişiklik <strong className="text-fg">Jurnal</strong> bölməsində qeydə alınır:
          kim, nə vaxt, nəyi dəyişdi.
        </p>
      </section>

      <section className="card rounded-3xl px-6 py-6" aria-labelledby="sections-title">
        <h2 id="sections-title" className="font-display text-lg font-bold">Bölmələr</h2>
        <ul className="mt-4 grid gap-3 md:grid-cols-2">
          {SECTIONS.map((section) => (
            <li key={section.to}>
              <Link to={section.to}
                className="block h-full rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10 transition-colors hover:bg-white/[0.07]">
                <p className="font-bold text-fg">{section.title}</p>
                <p className="mt-1 text-sm text-fg-2">{section.text}</p>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
