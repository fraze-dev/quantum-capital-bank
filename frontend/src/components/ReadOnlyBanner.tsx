import { useAuth } from '../auth/useAuth'

// Shown on staff pages to read-only auditors (the public demo's staff login).
export function ReadOnlyBanner() {
  const { user } = useAuth()
  if (user?.role !== 'Auditor') return null

  return (
    <p className="read-only-banner">
      <strong>Read-only access.</strong> You're signed in as an auditor: you can see every customer, account,
      transaction and admin action, but you can't make changes.
    </p>
  )
}
