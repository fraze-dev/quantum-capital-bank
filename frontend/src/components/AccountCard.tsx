import { useState, type FormEvent } from 'react'
import { deposit, withdraw } from '../api/accounts'
import { ApiError } from '../api/client'
import type { AccountSummary } from '../api/types'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

type Mode = 'idle' | 'deposit' | 'withdraw'

export function AccountCard({
  account,
  username,
  onUpdated,
}: {
  account: AccountSummary
  username: string
  onUpdated: (updated: AccountSummary) => void
}) {
  const [mode, setMode] = useState<Mode>('idle')
  const [amountInput, setAmountInput] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const badgeClass = account.accountType.toLowerCase().includes('check') ? 'checking' : ''

  function startMode(next: Mode) {
    setMode(next)
    setAmountInput('')
    setError(null)
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const amount = Number(amountInput)
    if (!Number.isFinite(amount) || amount <= 0) {
      setError('Enter an amount greater than 0.')
      return
    }

    setSubmitting(true)
    setError(null)
    try {
      const action = mode === 'deposit' ? deposit : withdraw
      const updated = await action(account.accountNumber, { username, amount })
      onUpdated(updated)
      startMode('idle')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to reach the server.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="account-card">
      <div className="account-card-top">
        <span className={`account-type-badge ${badgeClass}`}>{account.accountType}</span>
        <span className="account-number">{account.accountNumber}</span>
      </div>
      <span className="account-balance">{currencyFormatter.format(account.balance)}</span>

      {mode === 'idle' ? (
        <div className="account-actions">
          <button type="button" onClick={() => startMode('deposit')}>
            Deposit
          </button>
          <button type="button" onClick={() => startMode('withdraw')}>
            Withdraw
          </button>
        </div>
      ) : (
        <form className="inline-form" onSubmit={handleSubmit}>
          {error && <p className="form-error">{error}</p>}
          <input
            type="number"
            step="0.01"
            min="0.01"
            placeholder="Amount"
            value={amountInput}
            onChange={(e) => setAmountInput(e.target.value)}
            autoFocus
            required
          />
          <div className="account-actions">
            <button type="submit" disabled={submitting}>
              {mode === 'deposit' ? 'Confirm deposit' : 'Confirm withdrawal'}
            </button>
            <button type="button" onClick={() => startMode('idle')} disabled={submitting}>
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
