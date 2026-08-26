import type { Role } from '../api/types'

export function homePathForRole(role: Role): string {
  return role === 'Admin' ? '/admin' : '/accounts'
}
