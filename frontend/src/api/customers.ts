import { apiFetch } from './client'
import type { CreateCustomerRequest, CustomerSummary } from './types'

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
