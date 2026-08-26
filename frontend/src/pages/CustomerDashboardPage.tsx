import { useEffect, useState } from 'react'
import { useAuth } from '../auth/useAuth'
import { getAccountsByCustomer } from '../api/accounts'
import { AccountCard } from '../components/AccountCard'
import type { AccountSummary } from '../api/types'
import { ApiError } from '../api/client'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function CustomerDashboardPage() {
  const { user } = useAuth()
  const [accounts, setAccounts] = useState<AccountSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!user) return
    let cancelled = false

    getAccountsByCustomer(user.username)
      .then((result) => {
        if (!cancelled) setAccounts(result)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Unable to load accounts.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [user])

  const total = accounts.reduce((sum, account) => sum + account.balance, 0)

  return (
    <div className="page">
      <div className="page-header">
        <h1>Welcome back{user ? `, ${user.username}` : ''}</h1>
        <p>Here's an overview of your accounts.</p>
      </div>

      {loading && <p>Loading…</p>}
      {error && <p className="form-error">{error}</p>}

      {!loading && !error && (
        <>
          <div className="stat-row">
            <div className="stat-card">
              <span className="stat-label">Total balance</span>
              <span className="stat-value">{currencyFormatter.format(total)}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Accounts</span>
              <span className="stat-value">{accounts.length}</span>
            </div>
          </div>

          {accounts.length === 0 ? (
            <p className="empty-state">No accounts to show.</p>
          ) : (
            <div className="account-grid">
              {accounts.map((account) => (
                <AccountCard key={account.accountNumber} account={account} />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}
