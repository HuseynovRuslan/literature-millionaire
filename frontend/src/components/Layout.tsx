import { NavLink, Outlet } from 'react-router-dom'

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  [
    'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
    isActive
      ? 'bg-gold text-navy-900'
      : 'text-slate-300 hover:bg-navy-700 hover:text-white',
  ].join(' ')

export default function Layout() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-navy-600/60 bg-navy-800/80 backdrop-blur">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <NavLink to="/" className="flex items-center gap-2">
            <span className="text-2xl" aria-hidden>📚</span>
            <span className="text-lg font-semibold tracking-tight text-gold-light">
              Ədəbiyyat Milyonçusu
            </span>
          </NavLink>

          <nav className="flex items-center gap-1">
            <NavLink to="/" end className={navLinkClass}>
              Ana səhifə
            </NavLink>
            <NavLink to="/admin/questions" className={navLinkClass}>
              Admin
            </NavLink>
          </nav>
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        <Outlet />
      </main>

      <footer className="border-t border-navy-600/60 py-4 text-center text-xs text-slate-500">
        Azərbaycan ədəbiyyatı üzrə viktorina · MVP
      </footer>
    </div>
  )
}
