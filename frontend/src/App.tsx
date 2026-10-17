import { Navigate, Route, Routes } from 'react-router-dom'
import { NavBar } from './components/NavBar'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { CustomerDashboardPage } from './pages/CustomerDashboardPage'
import { AdminDashboardPage } from './pages/AdminDashboardPage'
import { CustomerDetailPage } from './pages/CustomerDetailPage'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { homePathForRole } from './auth/roleRouting'
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
              <ProtectedRoute role="Customer">
                <CustomerDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin"
            element={
              <ProtectedRoute role="Admin">
                <AdminDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin/customers/:username"
            element={
              <ProtectedRoute role="Admin">
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
