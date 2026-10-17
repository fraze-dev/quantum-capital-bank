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
