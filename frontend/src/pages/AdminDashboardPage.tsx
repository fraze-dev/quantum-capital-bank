import { useEffect, useState, type FormEvent } from 'react'
import { getCustomers } from '../api/customers'
import { getAccounts } from '../api/accounts'
import { AccountsTable } from '../components/AccountsTable'
import { AdminTransactions } from '../components/AdminTransactions'
import type { AccountSummary, CustomerSummary } from '../api/types'
import { ApiError } from '../api/client'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function AdminDashboardPage() {
  const [customers, setCustomers] = useState<CustomerSummary[]>([])
  const [allAccounts, setAllAccounts] = useState<AccountSummary[]>([])
  const [filteredAccounts, setFilteredAccounts] = useState<AccountSummary[]>([])
  const [minBalanceInput, setMinBalanceInput] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  function loadFilteredAccounts(minBalance?: number) {
    getAccounts(minBalance)
      .then(setFilteredAccounts)
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Unable to load accounts.'))
  }

  useEffect(() => {
    Promise.all([getCustomers(), getAccounts()])
      .then(([customerResult, accountResult]) => {
        setCustomers(customerResult)
        setAllAccounts(accountResult)
        setFilteredAccounts(accountResult)
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Unable to load data.'))
      .finally(() => setLoading(false))
  }, [])

  function handleFilterSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = minBalanceInput.trim() === '' ? undefined : Number(minBalanceInput)
    loadFilteredAccounts(Number.isNaN(parsed) ? undefined : parsed)
  }

  function clearFilter() {
    setMinBalanceInput('')
    loadFilteredAccounts()
  }

  const totalBalance = allAccounts.reduce((sum, account) => sum + account.balance, 0)

  return (
    <div className="page">
      <div className="page-header">
        <h1>Admin dashboard</h1>
        <p>Overview of all customers and accounts.</p>
      </div>

      {loading && <p>Loading…</p>}
      {error && <p className="form-error">{error}</p>}

      {!loading && (
        <>
          <div className="stat-row">
            <div className="stat-card">
              <span className="stat-label">Customers</span>
              <span className="stat-value">{customers.length}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Accounts</span>
              <span className="stat-value">{allAccounts.length}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">Total balance</span>
              <span className="stat-value">{currencyFormatter.format(totalBalance)}</span>
            </div>
          </div>

          <section>
            <h2>Customers</h2>
            {customers.length === 0 ? (
              <p className="empty-state">No customers to show.</p>
            ) : (
              <div className="section-card">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Username</th>
                      <th>Account numbers</th>
                    </tr>
                  </thead>
                  <tbody>
                    {customers.map((customer) => (
                      <tr key={customer.username}>
                        <td>{customer.username}</td>
                        <td>{customer.accountNumbers.join(', ') || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section>
            <h2>Accounts</h2>
            <form className="filter-form" onSubmit={handleFilterSubmit}>
              <label htmlFor="minBalance">Min balance</label>
              <input
                id="minBalance"
                type="number"
                value={minBalanceInput}
                onChange={(e) => setMinBalanceInput(e.target.value)}
                placeholder="0"
              />
              <button type="submit">Filter</button>
              <button type="button" onClick={clearFilter}>
                Clear
              </button>
            </form>
            <div className="section-card">
              <AccountsTable accounts={filteredAccounts} showOwner />
            </div>
          </section>

          <AdminTransactions customers={customers} />
        </>
      )}
    </div>
  )
}
