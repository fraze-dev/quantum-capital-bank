import type { Role } from '../api/types'

// Staff (admins and read-only auditors) use the admin pages.
export const STAFF_ROLES: Role[] = ['Admin', 'Auditor']

export function isStaff(role: Role): boolean {
  return STAFF_ROLES.includes(role)
}

export function homePathForRole(role: Role): string {
  return isStaff(role) ? '/admin' : '/accounts'
}
