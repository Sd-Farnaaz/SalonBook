const rawBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5220';
const API_BASE_URL = rawBaseUrl.replace(/\/+$/, '').replace(/\/api$/, '');

export class ApiError extends Error {
  constructor(message, status, data) {
    super(message);
    this.status = status;
    this.data = data;
  }
}

async function handleResponse(response) {
  const isJson = response.headers.get('content-type')?.includes('application/json');
  let data = null;
  if (isJson) {
    try {
      data = await response.json();
    } catch {
      data = null;
    }
  } else {
    const text = await response.text();
    if (text) data = { message: text };
  }

  if (!response.ok) {
    let message = 'An unexpected error occurred';
    if (data && data.message) message = data.message;
    else if (data && data.detail) message = data.detail;
    else if (data && data.title) message = data.title;
    else if (data && data.errors) {
      const errors = Object.values(data.errors).flat();
      message = errors[0] || 'Validation failed';
    } else if (response.status === 401) {
      message = 'Invalid email or password.';
    } else if (response.status === 403) {
      message = 'Forbidden';
    } else if (response.status === 429) {
      message = 'Too many requests. Please try again later.';
    }

    throw new ApiError(message, response.status, data);
  }

  return data;
}

// Mutex promise to prevent token-refresh race conditions
let refreshPromise = null;

async function refreshAuthTokens() {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    try {
      const refreshToken = localStorage.getItem('refreshToken');
      if (!refreshToken) {
        throw new Error('No refresh token available');
      }

      const userRole = localStorage.getItem('userRole')?.toLowerCase() || 'user';
      const roleSegment = userRole === 'admin' ? 'admin' : userRole === 'superadmin' ? 'superadmin' : 'user';

      const response = await fetch(`${API_BASE_URL}/api/auth/${roleSegment}/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken })
      });

      if (!response.ok) {
        // Refresh failed, clear session
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        localStorage.removeItem('userRole');
        window.dispatchEvent(new Event('session-expired'));
        throw new ApiError('Session expired. Please log in again.', 401, null);
      }

      const data = await response.json();
      localStorage.setItem('accessToken', data.accessToken);
      localStorage.setItem('refreshToken', data.refreshToken);
      if (data.user?.role) {
        localStorage.setItem('userRole', data.user.role);
      }
      return data.accessToken;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export async function fetchApi(endpoint, options = {}) {
  const { skipAuth = false, _retry = false, ...fetchOptions } = options;
  const token = !skipAuth ? localStorage.getItem('accessToken') : null;
  const isGetOrHead = !fetchOptions.method || fetchOptions.method.toUpperCase() === 'GET' || fetchOptions.method.toUpperCase() === 'HEAD';

  const headers = {
    ...(!isGetOrHead || fetchOptions.body ? { 'Content-Type': 'application/json' } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...fetchOptions.headers,
  };

  const config = {
    ...fetchOptions,
    headers,
  };

  if (config.body && typeof config.body !== 'string') {
    config.body = JSON.stringify(config.body);
  }

  const isPublicAuthEndpoint =
    endpoint.includes('/login') ||
    endpoint.includes('/register') ||
    endpoint.includes('/refresh') ||
    endpoint.includes('/forgot-password') ||
    endpoint.includes('/reset-password');

  try {
    const response = await fetch(`${API_BASE_URL}${endpoint}`, config);

    // Auto-refresh token if 401 and we have a refresh token (only for authenticated API calls)
    if (response.status === 401 && !_retry && !skipAuth && !isPublicAuthEndpoint && localStorage.getItem('refreshToken')) {
      try {
        const currentStoredToken = localStorage.getItem('accessToken');
        let newAccessToken;
        if (currentStoredToken && currentStoredToken !== token) {
          newAccessToken = currentStoredToken;
        } else {
          newAccessToken = await refreshAuthTokens();
        }
        const retryHeaders = {
          ...headers,
          Authorization: `Bearer ${newAccessToken}`,
        };
        const retryConfig = {
          ...fetchOptions,
          headers: retryHeaders,
        };
        if (retryConfig.body && typeof retryConfig.body !== 'string') {
          retryConfig.body = JSON.stringify(retryConfig.body);
        }
        const retryResponse = await fetch(`${API_BASE_URL}${endpoint}`, retryConfig);
        return handleResponse(retryResponse);
      } catch (refreshErr) {
        if (refreshErr instanceof ApiError) throw refreshErr;
        throw new ApiError('Session expired. Please log in again.', 401, null);
      }
    }

    return handleResponse(response);
  } catch (error) {
    if (error instanceof ApiError) throw error;
    throw new ApiError('Network error. Ensure the server is running.', 0, null);
  }
}
