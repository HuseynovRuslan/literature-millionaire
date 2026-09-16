/** Thrown by submitAnswer when the backend no longer accepts the session; the UI has already switched to the expired screen. */
export class SessionInvalidError extends Error {
  constructor() {
    super('Game session is no longer valid.')
    this.name = 'SessionInvalidError'
  }
}

/**
 * Thrown by submitTimeout when the server's clock still shows time left on the visible question.
 * The kiosk clock is simply a little ahead of the server's; the deadline itself is unchanged and the
 * answer buttons are already locked, so the caller just asks again in a moment. Not a failure, and
 * deliberately distinct from a network error so the player is never shown one.
 */
export class QuestionTimeRemainingError extends Error {
  constructor() {
    super('The question deadline has not passed on the server yet.')
    this.name = 'QuestionTimeRemainingError'
  }
}
