import { Navigate, Route, Routes } from 'react-router-dom'
import { NavBar } from './components/NavBar'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { CustomerDashboardPage } from './pages/CustomerDashboardPage'
import { AdminDashboardPage } from './pages/AdminDashboardPage'
import { CustomerDetailPage } from './pages/CustomerDetailPage'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { STAFF_ROLES, homePathForRole } from './auth/roleRouting'
import { useAuth } from './auth/useAuth'
import './App.css'

function HomeRedirect() {
  const { user } = useAuth()
  return <Navigate to={user ? homePathForRole(user.role) : '/login'} replace />
}

function App() {
  return (
    <>
      <NavBar />
      <main>
        <Routes>
          <Route path="/" element={<HomeRedirect />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route
            path="/accounts"
            element={
              <ProtectedRoute roles={['Customer']}>
                <CustomerDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin"
            element={
              <ProtectedRoute roles={STAFF_ROLES}>
                <AdminDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin/customers/:username"
            element={
              <ProtectedRoute roles={STAFF_ROLES}>
                <CustomerDetailPage />
              </ProtectedRoute>
            }
          />
          <Route path="*" element={<HomeRedirect />} />
        </Routes>
      </main>
    </>
  )
}

export default App
