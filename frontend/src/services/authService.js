import { fetchApi } from './apiClient';

export const authService = {
  // SuperAdmin
  superAdminLogin: (credentials) => fetchApi('/api/auth/superadmin/login', { method: 'POST', body: credentials, skipAuth: true }),
  superAdminLogout: (refreshToken) => fetchApi('/api/auth/superadmin/logout', { method: 'POST', body: { refreshToken } }),

  // Admin
  adminRegister: (data) => fetchApi('/api/auth/admin/register', { method: 'POST', body: data, skipAuth: true }),
  adminLogin: (credentials) => fetchApi('/api/auth/admin/login', { method: 'POST', body: credentials, skipAuth: true }),
  adminLogout: (refreshToken) => fetchApi('/api/auth/admin/logout', { method: 'POST', body: { refreshToken } }),

  // User
  userRegister: (data) => fetchApi('/api/auth/user/register', { method: 'POST', body: data, skipAuth: true }),
  userLogin: (credentials) => fetchApi('/api/auth/user/login', { method: 'POST', body: credentials, skipAuth: true }),
  userLogout: (refreshToken) => fetchApi('/api/auth/user/logout', { method: 'POST', body: { refreshToken } }),

  // Shared
  getMe: () => fetchApi('/api/auth/me', { method: 'GET' }),
  forgotPassword: (email) => fetchApi('/api/auth/forgot-password', { method: 'POST', body: { email }, skipAuth: true }),
  resetPassword: (data) => fetchApi('/api/auth/reset-password', { method: 'POST', body: data, skipAuth: true }),
  changePassword: (data) => fetchApi('/api/auth/change-password', { method: 'POST', body: data }),
};
