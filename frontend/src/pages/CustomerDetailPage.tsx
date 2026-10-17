import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getAccountsByCustomer } from '../api/accounts'
import { ApiError } from '../api/client'
import { changeCustomerStatus, getCustomer } from '../api/customers'
import type { AccountSummary, CustomerSummary } from '../api/types'
import { AdminAccountsTable } from '../components/AdminAccountsTable'
import { AdminAuditLog } from '../components/AdminAuditLog'
import { AdminTransactions } from '../components/AdminTransactions'
import { customerStatusAction } from '../components/customerStatusAction'
import { ReasonPrompt } from '../components/ReasonPrompt'
import { StatusBadge } from '../components/StatusBadge'
import { formatCurrency } from '../format'

// Admin view of one customer: status, accounts, transactions and the admin
// actions taken on them. Keyed by username so moving between customers
// starts from a clean slate.
export function CustomerDetailPage() {
  const { username = '' } = useParams()
  return <CustomerDetail key={username} username={username} />
}

function CustomerDetail({ username }: { username: string }) {
  const [customer, setCustomer] = useState<CustomerSummary | null>(null)
  const [accounts, setAccounts] = useState<AccountSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [changingStatus, setChangingStatus] = useState(false)
  const [auditVersion, setAuditVersion] = useState(0)

  useEffect(() => {
    let cancelled = false

    Promise.all([getCustomer(username), getAccountsByCustomer(username)])
      .then(([customerResult, accountResult]) => {
        if (cancelled) return
        setCustomer(customerResult)
        setAccounts(accountResult)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Unable to load this customer.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [username])

  function handleAccountChanged(updated: AccountSummary) {
    setAccounts((list) => list.map((account) => (account.accountNumber === updated.accountNumber ? updated : account)))
    setAuditVersion((version) => version + 1)
  }

  const statusAction = customer ? customerStatusAction(customer.status) : null

  async function confirmStatusChange(reason: string) {
    if (!statusAction) return
    const updated = await changeCustomerStatus(username, { status: statusAction.nextStatus, reason })
    setCustomer(updated)
    setChangingStatus(false)
    setAuditVersion((version) => version + 1)
  }

  const totalBalance = accounts.reduce((sum, account) => sum + account.balance, 0)
  const activeAccounts = accounts.filter((account) => account.status === 'Active').length

  return (
    <div className="page">
      <Link to="/admin" className="back-link">
        ← Admin dashboard
      </Link>

      {loading && <p>Loading…</p>}
      {error && <p className="form-error">{error}</p>}

      {customer && statusAction && (
        <>
          <div className="detail-header">
            <div className="page-header">
              <div className="detail-title">
                <h1>{customer.username}</h1>
                <StatusBadge status={customer.status} />
              </div>
              <p>Customer accounts, activity and admin history.</p>
            </div>
            {!changingStatus && (
              <button type="button" className="secondary" onClick={() => setChangingStatus(true)}>
                {statusAction.label}
              </button>
            )}
          </div>

          {changingStatus && (
            <div className="panel">
              <ReasonPrompt
                title={`${statusAction.label} for ${customer.username}`}
                confirmLabel={statusAction.confirmLabel}
                onConfirm={confirmStatusChange}
                onCancel={() => setChangingStatus(false)}
              />
            </div>
          )}

          <div className="stat-row">
            <div className="stat-card">
              <span className="stat-label">Total balance</span>
              <span className="stat-value">{formatCurrency(totalBalance)}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Accounts</span>
              <span className="stat-value">{accounts.length}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Active accounts</span>
              <span className="stat-value">{activeAccounts}</span>
            </div>
          </div>

          <section>
            <h2>Accounts</h2>
            <div className="section-card table-scroll">
              <AdminAccountsTable accounts={accounts} onChanged={handleAccountChanged} showOwner={false} />
            </div>
          </section>

          <AdminTransactions customer={username} />
          <AdminAuditLog username={username} reloadKey={auditVersion} />
        </>
      )}
    </div>
  )
}
