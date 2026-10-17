import { useEffect, useState, type FormEvent } from 'react'
import { getCustomers } from '../api/customers'
import { getAccounts } from '../api/accounts'
import { AdminAccountsTable } from '../components/AdminAccountsTable'
import { AdminAuditLog } from '../components/AdminAuditLog'
import { AdminCustomersTable } from '../components/AdminCustomersTable'
import { AdminTransactions } from '../components/AdminTransactions'
import { ReadOnlyBanner } from '../components/ReadOnlyBanner'
import type { AccountSummary, CustomerSummary } from '../api/types'
import { ApiError } from '../api/client'
import { formatCurrency } from '../format'

export function AdminDashboardPage() {
  const [customers, setCustomers] = useState<CustomerSummary[]>([])
  const [allAccounts, setAllAccounts] = useState<AccountSummary[]>([])
  const [filteredAccounts, setFilteredAccounts] = useState<AccountSummary[]>([])
  const [minBalanceInput, setMinBalanceInput] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  // Bumped after every status change so the audit log reloads.
  const [auditVersion, setAuditVersion] = useState(0)

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

  function handleAccountChanged(updated: AccountSummary) {
    const replace = (list: AccountSummary[]) =>
      list.map((account) => (account.accountNumber === updated.accountNumber ? updated : account))
    setAllAccounts(replace)
    setFilteredAccounts(replace)
    setAuditVersion((version) => version + 1)
  }

  function handleCustomerChanged(updated: CustomerSummary) {
    setCustomers((list) => list.map((customer) => (customer.username === updated.username ? updated : customer)))
    setAuditVersion((version) => version + 1)
  }

  const totalBalance = allAccounts.reduce((sum, account) => sum + account.balance, 0)

  return (
    <div className="page">
      <div className="page-header">
        <h1>Admin dashboard</h1>
        <p>Overview of all customers and accounts.</p>
      </div>
      <ReadOnlyBanner />

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
              <span className="stat-value">{formatCurrency(totalBalance)}</span>
            </div>
          </div>

          <section>
            <h2>Customers</h2>
            <div className="section-card table-scroll">
              <AdminCustomersTable customers={customers} onChanged={handleCustomerChanged} />
            </div>
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
            <div className="section-card table-scroll">
              <AdminAccountsTable accounts={filteredAccounts} onChanged={handleAccountChanged} />
            </div>
          </section>

          <AdminTransactions customers={customers} />
          <AdminAuditLog reloadKey={auditVersion} />
        </>
      )}
    </div>
  )
}
