import { clearSession, readSession } from './session'

const BASE_URL = import.meta.env.VITE_API_BASE_URL as string

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// Set by AuthProvider so an expired or rejected token logs the user out.
let onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const token = readSession()?.token

  const response = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options?.headers,
    },
  })

  // A 401 on a request that carried a token means the session is no longer
  // valid. (Without a token, e.g. a wrong password at login, it's a normal error.)
  if (response.status === 401 && token) {
    clearSession()
    onUnauthorized?.()
  }

  if (!response.ok) {
    const text = await response.text()
    throw new ApiError(response.status, text || response.statusText)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
