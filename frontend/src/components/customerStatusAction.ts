import type { UserStatus } from '../api/types'

// What the admin's enable/disable control does for a customer in a given status.
export function customerStatusAction(status: UserStatus) {
  return status === 'Active'
    ? { nextStatus: 'Disabled' as UserStatus, label: 'Disable login', confirmLabel: 'Disable' }
    : { nextStatus: 'Active' as UserStatus, label: 'Enable login', confirmLabel: 'Enable' }
}
