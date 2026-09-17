import { useOutletContext } from 'react-router-dom'
import type { AdminSession } from '../../api/admin'

/** What every admin page receives from the panel frame (AdminApp). */
export interface AdminOutletContext {
  session: AdminSession
  /** Call when a request answers 401/403: the panel returns to its sign-in screen. */
  onSignedOut: () => void
}

export function useAdmin() {
  return useOutletContext<AdminOutletContext>()
}
