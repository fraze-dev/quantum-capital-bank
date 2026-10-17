import type { AccountStatus, UserStatus } from '../api/types'

// Small colored label for an account or customer status.
export function StatusBadge({ status }: { status: AccountStatus | UserStatus }) {
  return <span className={`status-badge ${status.toLowerCase()}`}>{status}</span>
}
