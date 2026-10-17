import { useEffect, useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { getTransactions } from '../api/transactions'
import type { CustomerSummary, TransactionFilters, TransactionSummary } from '../api/types'
import { describeTransaction, formatCurrency, formatDateTime, formatSignedCurrency } from '../format'
import { CustomerLink } from './CustomerLink'

const LIMIT = 100

interface FilterForm {
  username: string
  accountNumber: string
  fromDate: string
  toDate: string
}

const emptyForm: FilterForm = { username: '', accountNumber: '', fromDate: '', toDate: '' }

// <input type="date"> gives "YYYY-MM-DD". Turn it into the instant that day
// starts in the viewer's time zone, optionally shifted by whole days.
function startOfLocalDay(date: string, addDays = 0): string {
  const [year, month, day] = date.split('-').map(Number)
  return new Date(year, month - 1, day + addDays).toISOString()
}

function toFilters(form: FilterForm): TransactionFilters {
  return {
    username: form.username || undefined,
    accountNumber: form.accountNumber.trim() || undefined,
    from: form.fromDate ? startOfLocalDay(form.fromDate) : undefined,
    // The "to" date is inclusive for the user, so stop at the start of the next day.
    to: form.toDate ? startOfLocalDay(form.toDate, 1) : undefined,
    limit: LIMIT,
  }
}

// All transactions with filters. With `customer` set, the list is locked to
// that customer (no customer picker, no owner column).
export function AdminTransactions({ customers = [], customer }: { customers?: CustomerSummary[]; customer?: string }) {
  const initialForm: FilterForm = { ...emptyForm, username: customer ?? '' }
  const [form, setForm] = useState<FilterForm>(initialForm)
  const [filters, setFilters] = useState<TransactionFilters>(() => toFilters(initialForm))
  const [transactions, setTransactions] = useState<TransactionSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    getTransactions(filters)
      .then((result) => {
        if (cancelled) return
        setTransactions(result)
        setError(null)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Unable to load transactions.')
      })

    return () => {
      cancelled = true
    }
  }, [filters])

  function update(field: keyof FilterForm, value: string) {
    setForm((prev) => ({ ...prev, [field]: value }))
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setFilters(toFilters(form))
  }

  function clearFilters() {
    setForm(initialForm)
    setFilters(toFilters(initialForm))
  }

  return (
    <section>
      <h2>Transactions</h2>
      <form className="filter-form" onSubmit={handleSubmit}>
        {!customer && (
          <select aria-label="Customer" value={form.username} onChange={(e) => update('username', e.target.value)}>
            <option value="">All customers</option>
            {customers.map((c) => (
              <option key={c.username} value={c.username}>
                {c.username}
              </option>
            ))}
          </select>
        )}
        <input
          aria-label="Account number"
          placeholder="Account number"
          value={form.accountNumber}
          onChange={(e) => update('accountNumber', e.target.value)}
        />
        <label htmlFor="txFrom">From</label>
        <input id="txFrom" type="date" value={form.fromDate} onChange={(e) => update('fromDate', e.target.value)} />
        <label htmlFor="txTo">To</label>
        <input id="txTo" type="date" value={form.toDate} onChange={(e) => update('toDate', e.target.value)} />
        <button type="submit">Filter</button>
        <button type="button" onClick={clearFilters}>
          Clear
        </button>
      </form>

      {error && <p className="form-error">{error}</p>}

      <div className="section-card table-scroll">
        {transactions === null ? (
          <p className="empty-state">Loading…</p>
        ) : transactions.length === 0 ? (
          <p className="empty-state">No transactions match these filters.</p>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Account</th>
                {!customer && <th>Owner</th>}
                <th>Description</th>
                <th className="numeric">Amount</th>
                <th className="numeric">Balance after</th>
                <th>By</th>
              </tr>
            </thead>
            <tbody>
              {transactions.map((transaction) => (
                <tr key={transaction.id}>
                  <td>{formatDateTime(transaction.timestamp)}</td>
                  <td className="account-number">{transaction.accountNumber}</td>
                  {!customer && (
                    <td>
                      <CustomerLink username={transaction.ownerUsername} />
                    </td>
                  )}
                  <td>{describeTransaction(transaction)}</td>
                  <td className={`numeric activity-amount ${transaction.amount > 0 ? 'positive' : ''}`}>
                    {formatSignedCurrency(transaction.amount)}
                  </td>
                  <td className="numeric">{formatCurrency(transaction.balanceAfter)}</td>
                  <td>{transaction.performedBy}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      {transactions?.length === LIMIT && (
        <p className="activity-empty">Showing the newest {LIMIT}. Narrow the filters to see older ones.</p>
      )}
    </section>
  )
}
