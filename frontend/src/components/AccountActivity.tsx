import { useEffect, useState } from 'react'
import { ApiError } from '../api/client'
import { getAccountTransactions } from '../api/transactions'
import type { TransactionSummary } from '../api/types'
import { describeTransaction, formatDateTime, formatSignedCurrency } from '../format'

const RECENT_LIMIT = 5

// Last few transactions on one account. Reloads whenever the balance changes,
// so a deposit, withdrawal or transfer shows up right away.
export function AccountActivity({ accountNumber, balance }: { accountNumber: string; balance: number }) {
  const [transactions, setTransactions] = useState<TransactionSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    getAccountTransactions(accountNumber, RECENT_LIMIT)
      .then((result) => {
        if (cancelled) return
        setTransactions(result)
        setError(null)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Unable to load activity.')
      })

    return () => {
      cancelled = true
    }
  }, [accountNumber, balance])

  if (error) return <p className="form-error">{error}</p>
  if (transactions === null) return <p className="activity-empty">Loading…</p>
  if (transactions.length === 0) return <p className="activity-empty">No activity yet.</p>

  return (
    <ul className="activity-list">
      {transactions.map((transaction) => (
        <li key={transaction.id} className="activity-row">
          <div className="activity-details">
            <span className="activity-label">{describeTransaction(transaction)}</span>
            <span className="activity-date">{formatDateTime(transaction.timestamp)}</span>
          </div>
          <span className={`activity-amount ${transaction.amount > 0 ? 'positive' : ''}`}>
            {formatSignedCurrency(transaction.amount)}
          </span>
        </li>
      ))}
    </ul>
  )
}
