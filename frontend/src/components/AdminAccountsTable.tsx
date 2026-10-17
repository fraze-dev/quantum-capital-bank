import { Fragment, useState } from 'react'
import { changeAccountStatus } from '../api/accounts'
import type { AccountStatus, AccountSummary } from '../api/types'
import { formatCurrency } from '../format'
import { CustomerLink } from './CustomerLink'
import { ReasonPrompt } from './ReasonPrompt'
import { StatusBadge } from './StatusBadge'

const ACTION_LABELS: Record<AccountStatus, string> = {
  Frozen: 'Freeze',
  Active: 'Unfreeze',
  Closed: 'Close',
}

// The status changes an admin can make from each status. Closed is final.
const NEXT_STATUSES: Record<AccountStatus, AccountStatus[]> = {
  Active: ['Frozen', 'Closed'],
  Frozen: ['Active', 'Closed'],
  Closed: [],
}

export function AdminAccountsTable({
  accounts,
  onChanged,
  showOwner = true,
}: {
  accounts: AccountSummary[]
  onChanged: (updated: AccountSummary) => void
  showOwner?: boolean
}) {
  const columns = showOwner ? 6 : 5
  const [pending, setPending] = useState<{ accountNumber: string; status: AccountStatus } | null>(null)

  if (accounts.length === 0) {
    return <p className="empty-state">No accounts to show.</p>
  }

  async function confirm(reason: string) {
    if (!pending) return
    const updated = await changeAccountStatus(pending.accountNumber, { status: pending.status, reason })
    setPending(null)
    onChanged(updated)
  }

  return (
    <table className="data-table">
      <thead>
        <tr>
          <th>Account Number</th>
          {showOwner && <th>Owner</th>}
          <th>Type</th>
          <th className="numeric">Balance</th>
          <th>Status</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        {accounts.map((account) => (
          <Fragment key={account.accountNumber}>
            <tr>
              <td className="account-number">{account.accountNumber}</td>
              {showOwner && (
                <td>
                  <CustomerLink username={account.ownerUsername} />
                </td>
              )}
              <td>{account.accountType}</td>
              <td className="numeric">{formatCurrency(account.balance)}</td>
              <td>
                <StatusBadge status={account.status} />
              </td>
              <td className="row-actions">
                {NEXT_STATUSES[account.status].length === 0 && '—'}
                {NEXT_STATUSES[account.status].map((status) => {
                  const blocked = status === 'Closed' && account.balance !== 0
                  return (
                    <button
                      key={status}
                      type="button"
                      className="link-button"
                      disabled={blocked}
                      title={blocked ? 'Only accounts with a $0.00 balance can be closed.' : undefined}
                      onClick={() => setPending({ accountNumber: account.accountNumber, status })}
                    >
                      {ACTION_LABELS[status]}
                    </button>
                  )
                })}
              </td>
            </tr>
            {pending?.accountNumber === account.accountNumber && (
              <tr className="prompt-row">
                <td colSpan={columns}>
                  <ReasonPrompt
                    title={`${ACTION_LABELS[pending.status]} ${account.accountNumber}`}
                    confirmLabel={ACTION_LABELS[pending.status]}
                    onConfirm={confirm}
                    onCancel={() => setPending(null)}
                  />
                </td>
              </tr>
            )}
          </Fragment>
        ))}
      </tbody>
    </table>
  )
}
