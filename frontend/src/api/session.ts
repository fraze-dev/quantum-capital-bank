import type { LoginResponse } from './types'

// The logged-in user and their JWT live in sessionStorage, so they survive a
// page refresh but not closing the tab.
const STORAGE_KEY = 'bank_auth'

export function readSession(): LoginResponse | null {
  const raw = sessionStorage.getItem(STORAGE_KEY)
  if (!raw) return null
  try {
    const session = JSON.parse(raw) as LoginResponse
    if (!session.token || Date.parse(session.expiresAt) <= Date.now()) {
      clearSession()
      return null
    }
    return session
  } catch {
    return null
  }
}

export function saveSession(session: LoginResponse) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
}

export function clearSession() {
  sessionStorage.removeItem(STORAGE_KEY)
}
