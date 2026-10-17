import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'

// Inline "why are you doing this?" step for admin status changes. The
// reason is required and ends up in the audit log.
export function ReasonPrompt({
  title,
  confirmLabel,
  onConfirm,
  onCancel,
}: {
  title: string
  confirmLabel: string
  onConfirm: (reason: string) => Promise<void>
  onCancel: () => void
}) {
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!reason.trim()) {
      setError('A reason is required.')
      return
    }

    setSubmitting(true)
    setError(null)
    try {
      await onConfirm(reason.trim())
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to reach the server.')
      setSubmitting(false)
    }
  }

  return (
    <form className="reason-prompt" onSubmit={handleSubmit}>
      <span className="reason-prompt-title">{title}</span>
      {error && <p className="form-error">{error}</p>}
      <div className="inline-form-row-controls">
        <input
          aria-label="Reason"
          placeholder="Reason (recorded in the audit log)"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          maxLength={500}
          autoFocus
        />
        <button type="submit" disabled={submitting}>
          {submitting ? 'Saving…' : confirmLabel}
        </button>
        <button type="button" className="secondary" onClick={onCancel} disabled={submitting}>
          Cancel
        </button>
      </div>
    </form>
  )
}
