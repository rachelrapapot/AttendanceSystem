import { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { silentGet, silentPost } from '../api/client';
import { authApi } from '../api/auth';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  const fetchUser = useCallback(async () => {
    let data = await silentGet('/auth/me');

    if (!data) {
      const refreshed = await silentPost('/auth/refresh');
      if (refreshed !== null) {
        data = await silentGet('/auth/me');
      }
    }

    setUser(data);
    setLoading(false);
  }, []);

  useEffect(() => {
    fetchUser();

    const onSessionExpired = () => setUser(null);
    window.addEventListener('auth:sessionExpired', onSessionExpired);
    return () => window.removeEventListener('auth:sessionExpired', onSessionExpired);
  }, [fetchUser]);

  const login = async (email, password) => {
    await authApi.login(email, password);
    await fetchUser();
  };

  const logout = async () => {
    try {
      await authApi.logout();
    } finally {
      setUser(null);
    }
  };

  return (
    <AuthContext.Provider value={{ user, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
