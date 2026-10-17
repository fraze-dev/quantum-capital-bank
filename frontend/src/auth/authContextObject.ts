import { createContext } from 'react'
import type { LoginResponse } from '../api/types'

export interface AuthContextValue {
  user: LoginResponse | null
  login: (username: string, password: string) => Promise<LoginResponse>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
