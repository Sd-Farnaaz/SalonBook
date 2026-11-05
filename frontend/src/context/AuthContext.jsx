import { createContext, useContext, useState, useEffect } from 'react';
import { authService } from '../services/authService';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const initializeAuth = async () => {
      const token = localStorage.getItem('accessToken');
      if (token) {
        try {
          const userData = await authService.getMe();
          setUser(userData);
        } catch (error) {
          console.error("Session restoration failed:", error);
          clearSession();
        }
      }
      setLoading(false);
    };

    initializeAuth();

    const handleSessionExpired = () => {
      setUser(null);
    };
    const handleStorageChange = (e) => {
      if ((e.key === 'accessToken' || e.key === 'userRole') && !e.newValue) {
        setUser(null);
      }
    };
    window.addEventListener('session-expired', handleSessionExpired);
    window.addEventListener('storage', handleStorageChange);
    return () => {
      window.removeEventListener('session-expired', handleSessionExpired);
      window.removeEventListener('storage', handleStorageChange);
    };
  }, []);

  const login = (authResponse) => {
    localStorage.setItem('accessToken', authResponse.accessToken);
    localStorage.setItem('refreshToken', authResponse.refreshToken);
    localStorage.setItem('userRole', authResponse.user.role);
    setUser(authResponse.user);
  };

  const clearSession = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('userRole');
    setUser(null);
  };

  const logout = async () => {
    const refreshToken = localStorage.getItem('refreshToken');
    const role = user?.role?.toLowerCase();
    
    if (refreshToken && role) {
      try {
        if (role === 'superadmin') await authService.superAdminLogout(refreshToken);
        else if (role === 'admin') await authService.adminLogout(refreshToken);
        else await authService.userLogout(refreshToken);
      } catch (error) {
        console.error("Logout API failed:", error);
      }
    }
    clearSession();
  };

  return (
    <AuthContext.Provider value={{ user, loading, login, logout, clearSession }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within an AuthProvider");
  return context;
}
