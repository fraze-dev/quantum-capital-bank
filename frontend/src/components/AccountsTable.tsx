import type { AccountSummary } from '../api/types'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function AccountsTable({ accounts, showOwner = false }: { accounts: AccountSummary[]; showOwner?: boolean }) {
  if (accounts.length === 0) {
    return <p className="empty-state">No accounts to show.</p>
  }

  return (
    <table className="data-table">
      <thead>
        <tr>
          <th>Account Number</th>
          {showOwner && <th>Owner</th>}
          <th>Type</th>
          <th>Balance</th>
        </tr>
      </thead>
      <tbody>
        {accounts.map((account) => (
          <tr key={account.accountNumber}>
            <td>{account.accountNumber}</td>
            {showOwner && <td>{account.ownerUsername}</td>}
            <td>{account.accountType}</td>
            <td>{currencyFormatter.format(account.balance)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
