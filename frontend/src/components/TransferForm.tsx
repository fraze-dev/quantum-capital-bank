import { useState, type FormEvent } from 'react'
import { transfer } from '../api/accounts'
import { ApiError } from '../api/client'
import type { AccountSummary } from '../api/types'

export function TransferForm({
  username,
  accounts,
  onTransferred,
}: {
  username: string
  accounts: AccountSummary[]
  onTransferred: (from: AccountSummary, to: AccountSummary) => void
}) {
  const [fromAccountNumber, setFromAccountNumber] = useState(accounts[0]?.accountNumber ?? '')
  const [toAccountNumber, setToAccountNumber] = useState(accounts[1]?.accountNumber ?? '')
  const [amountInput, setAmountInput] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // Fall back to real account numbers if the selection is stale (e.g. a new
  // account was just opened) rather than syncing state via an effect.
  const numbers = accounts.map((account) => account.accountNumber)
  const from = numbers.includes(fromAccountNumber) ? fromAccountNumber : (numbers[0] ?? '')
  const to =
    numbers.includes(toAccountNumber) && toAccountNumber !== from
      ? toAccountNumber
      : (numbers.find((number) => number !== from) ?? '')

  if (accounts.length < 2) {
    return null
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const amount = Number(amountInput)
    if (!Number.isFinite(amount) || amount <= 0) {
      setError('Enter an amount greater than 0.')
      return
    }
    if (from === to) {
      setError('Choose two different accounts.')
      return
    }

    setSubmitting(true)
    setError(null)
    try {
      const result = await transfer({ username, fromAccountNumber: from, toAccountNumber: to, amount })
      onTransferred(result.from, result.to)
      setAmountInput('')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to reach the server.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="panel inline-form-row" onSubmit={handleSubmit}>
      <h2>Transfer between your accounts</h2>
      {error && <p className="form-error">{error}</p>}
      <div className="inline-form-row-controls">
        <select value={from} onChange={(e) => setFromAccountNumber(e.target.value)}>
          {accounts.map((account) => (
            <option key={account.accountNumber} value={account.accountNumber}>
              From: {account.accountNumber}
            </option>
          ))}
        </select>
        <select value={to} onChange={(e) => setToAccountNumber(e.target.value)}>
          {accounts.map((account) => (
            <option key={account.accountNumber} value={account.accountNumber}>
              To: {account.accountNumber}
            </option>
          ))}
        </select>
        <input
          type="number"
          step="0.01"
          min="0.01"
          placeholder="Amount"
          value={amountInput}
          onChange={(e) => setAmountInput(e.target.value)}
          required
        />
        <button type="submit" disabled={submitting}>
          {submitting ? 'Transferring…' : 'Transfer'}
        </button>
      </div>
    </form>
  )
}
