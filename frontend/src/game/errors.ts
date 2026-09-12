/** Thrown by submitAnswer when the backend no longer accepts the session; the UI has already switched to the expired screen. */
export class SessionInvalidError extends Error {
  constructor() {
    super('Game session is no longer valid.')
    this.name = 'SessionInvalidError'
  }
}
