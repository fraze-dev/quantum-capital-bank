import { apiFetch } from './client'
import type { AuditEntry } from './types'

// Admin only. Newest first.
export function getAuditLog(limit = 50): Promise<AuditEntry[]> {
  return apiFetch<AuditEntry[]>(`/audit?limit=${limit}`)
}
