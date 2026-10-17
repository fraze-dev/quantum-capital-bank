import { Link } from 'react-router-dom'

// A customer's name as a link to their admin detail page.
export function CustomerLink({ username }: { username: string }) {
  return <Link to={`/admin/customers/${encodeURIComponent(username)}`}>{username}</Link>
}
