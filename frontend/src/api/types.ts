export type Role = 'Admin' | 'Customer'

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginResponse {
  username: string
  role: Role
  token: string
  expiresAt: string
}

export interface CustomerSummary {
  username: string
  accountNumbers: string[]
}

export interface CreateCustomerRequest {
  username: string
  password: string
}

export interface AccountSummary {
  accountNumber: string
  ownerUsername: string
  accountType: string
  balance: number
}

export type AccountType = 'Savings' | 'Checking'

export interface OpenAccountRequest {
  ownerUsername: string
  accountType: AccountType
}

export interface AmountRequest {
  amount: number
}

export interface TransferRequest {
  fromAccountNumber: string
  toAccountNumber: string
  amount: number
}

export interface TransferResponse {
  from: AccountSummary
  to: AccountSummary
}

export type TransactionType = 'Deposit' | 'Withdrawal' | 'TransferIn' | 'TransferOut' | 'OpeningBalance'

export interface TransactionSummary {
  id: string
  accountNumber: string
  ownerUsername: string
  type: TransactionType
  // Signed: positive for money in, negative for money out.
  amount: number
  balanceAfter: number
  counterpartyAccountNumber: string | null
  performedBy: string
  timestamp: string
}

// from is inclusive, to is exclusive (ISO 8601 instants).
export interface TransactionFilters {
  username?: string
  accountNumber?: string
  from?: string
  to?: string
  limit?: number
}
