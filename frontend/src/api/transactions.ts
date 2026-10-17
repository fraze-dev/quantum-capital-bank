import { apiFetch } from './client'
import type { TransactionFilters, TransactionSummary } from './types'

export function getAccountTransactions(accountNumber: string, limit?: number): Promise<TransactionSummary[]> {
  const query = limit != null ? `?limit=${limit}` : ''
  return apiFetch<TransactionSummary[]>(`/accounts/${encodeURIComponent(accountNumber)}/transactions${query}`)
}

// Admin only.
export function getTransactions(filters: TransactionFilters): Promise<TransactionSummary[]> {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) {
    if (value != null && value !== '') params.set(key, String(value))
  }
  const query = params.size > 0 ? `?${params}` : ''
  return apiFetch<TransactionSummary[]>(`/transactions${query}`)
}
