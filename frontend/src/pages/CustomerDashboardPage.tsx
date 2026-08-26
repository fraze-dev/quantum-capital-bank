import { useEffect, useState } from 'react'
import { useAuth } from '../auth/useAuth'
import { getAccountsByCustomer } from '../api/accounts'
import { AccountsTable } from '../components/AccountsTable'
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
      <h1>Your accounts</h1>
      {loading && <p>Loading…</p>}
      {error && <p className="form-error">{error}</p>}
      {!loading && !error && (
        <>
          <p className="summary-total">Total balance: {currencyFormatter.format(total)}</p>
          <AccountsTable accounts={accounts} />
        </>
      )}
    </div>
  )
}
