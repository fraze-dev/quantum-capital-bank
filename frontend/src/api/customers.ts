import { apiFetch } from './client'
import type { CreateCustomerRequest, CustomerSummary, StatusChangeRequest, UserStatus } from './types'

export function getCustomers(): Promise<CustomerSummary[]> {
  return apiFetch<CustomerSummary[]>('/customers')
}

export function getCustomer(username: string): Promise<CustomerSummary> {
  return apiFetch<CustomerSummary>(`/customers/${encodeURIComponent(username)}`)
}

export function createCustomer(request: CreateCustomerRequest): Promise<CustomerSummary> {
  return apiFetch<CustomerSummary>('/customers', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

// Admin only: disable or re-enable a customer's login.
export function changeCustomerStatus(
  username: string,
  request: StatusChangeRequest<UserStatus>,
): Promise<CustomerSummary> {
  return apiFetch<CustomerSummary>(`/customers/${encodeURIComponent(username)}/status`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}
