import { useEffect, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { login as loginRequest } from '../api/auth'
import { setUnauthorizedHandler } from '../api/client'
import { clearSession, readSession, saveSession } from '../api/session'
import type { LoginResponse } from '../api/types'
import { AuthContext } from './authContextObject'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<LoginResponse | null>(readSession)
  const navigate = useNavigate()

  useEffect(() => {
    setUnauthorizedHandler((message) => {
      setUser(null)
      navigate('/login', { replace: true, state: { notice: message ?? 'Your session expired. Please log in again.' } })
    })
    return () => setUnauthorizedHandler(null)
  }, [navigate])

  async function login(username: string, password: string): Promise<LoginResponse> {
    const response = await loginRequest({ username, password })
    saveSession(response)
    setUser(response)
    return response
  }

  function logout() {
    clearSession()
    setUser(null)
  }

  return <AuthContext.Provider value={{ user, login, logout }}>{children}</AuthContext.Provider>
}
