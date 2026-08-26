import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { homePathForRole } from '../auth/roleRouting'

export function NavBar() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <nav className="nav-bar">
      <Link to={user ? homePathForRole(user.role) : '/login'} className="nav-brand">
        Bank
      </Link>
      {user && (
        <div className="nav-user">
          <span>
            {user.username} <span className="nav-role">({user.role})</span>
          </span>
          <button type="button" onClick={handleLogout}>
            Log out
          </button>
        </div>
      )}
    </nav>
  )
}
