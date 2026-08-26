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
      <div className="nav-inner">
        <Link to={user ? homePathForRole(user.role) : '/login'} className="nav-brand">
          Quantum Capital
        </Link>
        {user && (
          <div className="nav-user">
            <span className="nav-username">
              <span className="nav-avatar">{user.username.slice(0, 1)}</span>
              {user.username} <span className="nav-role">({user.role})</span>
            </span>
            <button type="button" onClick={handleLogout}>
              Log out
            </button>
          </div>
        )}
      </div>
    </nav>
  )
}
