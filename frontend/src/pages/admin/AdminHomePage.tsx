import { useAdmin } from './adminContext'

/**
 * The panel's first page. Says plainly what exists and what does not yet, so nobody goes looking for a
 * campaign editor that is still being built.
 */
const COMING = [
  { title: 'Kitablar', text: 'Ayın kitabını əlavə etmək, üz qabığını yükləmək.' },
  { title: 'Suallar', text: 'Sualları axtarmaq, düzəltmək, şəkilli sual əlavə etmək, faylla toplu idxal.' },
  { title: 'Kateqoriyalar', text: 'Kateqoriyaların adı, sırası və aktivliyi.' },
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

      <section className="card rounded-3xl px-6 py-6" aria-labelledby="coming-title">
        <h2 id="coming-title" className="font-display text-lg font-bold">Hazırlanan bölmələr</h2>
        <ul className="mt-4 grid gap-3 md:grid-cols-2">
          {COMING.map((item) => (
            <li key={item.title} className="rounded-2xl bg-white/[0.04] px-4 py-3 ring-1 ring-white/10">
              <p className="font-bold text-fg">{item.title}</p>
              <p className="mt-1 text-sm text-fg-2">{item.text}</p>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
