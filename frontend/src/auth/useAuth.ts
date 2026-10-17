import { useContext } from 'react'
import { AuthContext, type AuthContextValue } from './authContextObject'

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
