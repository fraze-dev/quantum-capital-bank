import { apiFetch } from './client'
import type { AuditEntry } from './types'

// Admin only. Newest first. With a username, returns entries about that
// customer and about any of their accounts.
export function getAuditLog({ username, limit = 50 }: { username?: string; limit?: number } = {}): Promise<AuditEntry[]> {
  const params = new URLSearchParams({ limit: String(limit) })
  if (username) params.set('username', username)
  return apiFetch<AuditEntry[]>(`/audit?${params}`)
}
