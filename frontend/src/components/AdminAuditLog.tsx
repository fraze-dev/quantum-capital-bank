import { useEffect, useState } from 'react'
import { getAuditLog } from '../api/audit'
import { ApiError } from '../api/client'
import type { AuditEntry } from '../api/types'
import { describeAuditAction, formatDateTime } from '../format'

// Every admin status change, newest first. reloadKey changes whenever the
// admin makes a change on this page, so the log refreshes right away.
export function AdminAuditLog({ reloadKey }: { reloadKey: number }) {
  const [entries, setEntries] = useState<AuditEntry[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    getAuditLog()
      .then((result) => {
        if (cancelled) return
        setEntries(result)
        setError(null)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiError ? err.message : 'Unable to load the audit log.')
      })

    return () => {
      cancelled = true
    }
  }, [reloadKey])

  return (
    <section>
      <h2>Audit log</h2>
      {error && <p className="form-error">{error}</p>}
      <div className="section-card table-scroll">
        {entries === null ? (
          <p className="empty-state">Loading…</p>
        ) : entries.length === 0 ? (
          <p className="empty-state">No admin actions yet.</p>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>When</th>
                <th>Admin</th>
                <th>Action</th>
                <th>Target</th>
                <th>Change</th>
                <th>Reason</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry) => (
                <tr key={entry.id}>
                  <td>{formatDateTime(entry.timestamp)}</td>
                  <td>{entry.performedBy}</td>
                  <td>{describeAuditAction(entry)}</td>
                  <td className={entry.targetType === 'Account' ? 'account-number' : undefined}>{entry.targetId}</td>
                  <td>
                    {entry.fromStatus} → {entry.toStatus}
                  </td>
                  <td>{entry.reason}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </section>
  )
}
