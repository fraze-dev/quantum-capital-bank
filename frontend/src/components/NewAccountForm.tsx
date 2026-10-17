import { useState, type FormEvent } from 'react'
import { openAccount } from '../api/accounts'
import { ApiError } from '../api/client'
import type { AccountSummary, AccountType } from '../api/types'

export function NewAccountForm({
  username,
  onCreated,
}: {
  username: string
  onCreated: (created: AccountSummary) => void
}) {
  const [accountType, setAccountType] = useState<AccountType>('Savings')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      const created = await openAccount({ ownerUsername: username, accountType })
      onCreated(created)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to reach the server.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="panel inline-form-row" onSubmit={handleSubmit}>
      <h2>Open a new account</h2>
      {error && <p className="form-error">{error}</p>}
      <div className="inline-form-row-controls">
        <select value={accountType} onChange={(e) => setAccountType(e.target.value as AccountType)}>
          <option value="Savings">Savings</option>
          <option value="Checking">Checking</option>
        </select>
        <button type="submit" disabled={submitting}>
          {submitting ? 'Opening…' : 'Open account'}
        </button>
      </div>
    </form>
  )
}
