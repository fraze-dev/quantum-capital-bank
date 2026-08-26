import { useState, type ReactNode } from 'react'
import { login as loginRequest } from '../api/auth'
import type { LoginResponse } from '../api/types'
import { AuthContext } from './authContextObject'

const STORAGE_KEY = 'bank_auth'

function readStoredUser(): LoginResponse | null {
  const raw = sessionStorage.getItem(STORAGE_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as LoginResponse
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<LoginResponse | null>(readStoredUser)

  async function login(username: string, password: string): Promise<LoginResponse> {
    const response = await loginRequest({ username, password })
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(response))
    setUser(response)
    return response
  }

  function logout() {
    sessionStorage.removeItem(STORAGE_KEY)
    setUser(null)
  }

  return <AuthContext.Provider value={{ user, login, logout }}>{children}</AuthContext.Provider>
}
