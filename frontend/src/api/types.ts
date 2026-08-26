export type Role = 'Admin' | 'Customer'

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginResponse {
  username: string
  role: Role
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
