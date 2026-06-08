import { Navigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { NavBar } from './NavBar';

export function AdminRoute({ children }) {
  const { user, loading } = useAuth();

  if (loading) {
    return (
      <div className="loading-screen">
        <div className="spinner" />
        <p>Loading...</p>
      </div>
    );
  }

  if (!user) return <Navigate to="/login" replace />;

  if (user.role !== 'Admin') {
    return (
      <div className="app-layout">
        <NavBar />
        <div className="main-wrapper">
          <main className="main-content">
            <div className="access-denied-box">
              <div className="access-denied-icon">⛔</div>
              <h2>Access Denied</h2>
              <p>You do not have permission to access the Admin Panel.</p>
            </div>
          </main>
        </div>
      </div>
    );
  }

  return children;
}
