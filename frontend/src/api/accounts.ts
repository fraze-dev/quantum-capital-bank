import { apiFetch } from './client'
import type { AccountSummary } from './types'

export function getAccounts(minBalance?: number): Promise<AccountSummary[]> {
  const query = minBalance != null ? `?minBalance=${minBalance}` : ''
  return apiFetch<AccountSummary[]>(`/accounts${query}`)
}

export function getAccountsByCustomer(username: string): Promise<AccountSummary[]> {
  return apiFetch<AccountSummary[]>(`/accounts/customer/${encodeURIComponent(username)}`)
}
