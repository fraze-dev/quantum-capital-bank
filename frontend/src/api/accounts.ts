import { apiFetch } from './client'
import type {
  AccountStatus,
  AccountSummary,
  AmountRequest,
  OpenAccountRequest,
  StatusChangeRequest,
  TransferRequest,
  TransferResponse,
} from './types'

// Admin only: freeze, unfreeze or close an account.
export function changeAccountStatus(
  accountNumber: string,
  request: StatusChangeRequest<AccountStatus>,
): Promise<AccountSummary> {
  return apiFetch<AccountSummary>(`/accounts/${encodeURIComponent(accountNumber)}/status`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function getAccounts(minBalance?: number): Promise<AccountSummary[]> {
  const query = minBalance != null ? `?minBalance=${minBalance}` : ''
  return apiFetch<AccountSummary[]>(`/accounts${query}`)
}

export function getAccountsByCustomer(username: string): Promise<AccountSummary[]> {
  return apiFetch<AccountSummary[]>(`/accounts/customer/${encodeURIComponent(username)}`)
}

export function openAccount(request: OpenAccountRequest): Promise<AccountSummary> {
  return apiFetch<AccountSummary>('/accounts', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function deposit(accountNumber: string, request: AmountRequest): Promise<AccountSummary> {
  return apiFetch<AccountSummary>(`/accounts/${encodeURIComponent(accountNumber)}/deposit`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function withdraw(accountNumber: string, request: AmountRequest): Promise<AccountSummary> {
  return apiFetch<AccountSummary>(`/accounts/${encodeURIComponent(accountNumber)}/withdraw`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function transfer(request: TransferRequest): Promise<TransferResponse> {
  return apiFetch<TransferResponse>('/accounts/transfer', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}
