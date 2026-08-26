import type { AccountSummary } from '../api/types'

const currencyFormatter = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function AccountCard({ account }: { account: AccountSummary }) {
  const badgeClass = account.accountType.toLowerCase().includes('check') ? 'checking' : ''

  return (
    <div className="account-card">
      <div className="account-card-top">
        <span className={`account-type-badge ${badgeClass}`}>{account.accountType}</span>
        <span className="account-number">{account.accountNumber}</span>
      </div>
      <span className="account-balance">{currencyFormatter.format(account.balance)}</span>
    </div>
  )
}
