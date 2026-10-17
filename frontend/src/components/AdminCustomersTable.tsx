import { Fragment, useState } from 'react'
import { changeCustomerStatus } from '../api/customers'
import type { CustomerSummary } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { CustomerLink } from './CustomerLink'
import { customerStatusAction } from './customerStatusAction'
import { ReasonPrompt } from './ReasonPrompt'
import { StatusBadge } from './StatusBadge'

export function AdminCustomersTable({
  customers,
  onChanged,
}: {
  customers: CustomerSummary[]
  onChanged: (updated: CustomerSummary) => void
}) {
  const [pending, setPending] = useState<string | null>(null)
  // Auditors see the same table without the actions (the API refuses them anyway).
  const canChange = useAuth().user?.role === 'Admin'
  const columns = canChange ? 4 : 3

  if (customers.length === 0) {
    return <p className="empty-state">No customers to show.</p>
  }

  return (
    <table className="data-table">
      <thead>
        <tr>
          <th>Username</th>
          <th>Account numbers</th>
          <th>Status</th>
          {canChange && <th>Actions</th>}
        </tr>
      </thead>
      <tbody>
        {customers.map((customer) => {
          const { nextStatus, label: actionLabel, confirmLabel } = customerStatusAction(customer.status)

          async function confirm(reason: string) {
            const updated = await changeCustomerStatus(customer.username, { status: nextStatus, reason })
            setPending(null)
            onChanged(updated)
          }

          return (
            <Fragment key={customer.username}>
              <tr>
                <td>
                  <CustomerLink username={customer.username} />
                </td>
                <td>{customer.accountNumbers.join(', ') || '—'}</td>
                <td>
                  <StatusBadge status={customer.status} />
                </td>
                {canChange && (
                  <td className="row-actions">
                    <button type="button" className="link-button" onClick={() => setPending(customer.username)}>
                      {actionLabel}
                    </button>
                  </td>
                )}
              </tr>
              {pending === customer.username && (
                <tr className="prompt-row">
                  <td colSpan={columns}>
                    <ReasonPrompt
                      title={`${actionLabel} for ${customer.username}`}
                      confirmLabel={confirmLabel}
                      onConfirm={confirm}
                      onCancel={() => setPending(null)}
                    />
                  </td>
                </tr>
              )}
            </Fragment>
          )
        })}
      </tbody>
    </table>
  )
}
