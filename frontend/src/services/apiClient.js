const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5220';

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
    data = await response.json();
  } else {
    const text = await response.text();
    if (text) data = { message: text };
  }

  if (!response.ok) {
    let message = 'An unexpected error occurred';
    if (data && data.message) message = data.message;
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

export async function fetchApi(endpoint, options = {}) {
  const { skipAuth = false, ...fetchOptions } = options;
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

  try {
    const response = await fetch(`${API_BASE_URL}${endpoint}`, config);
    
    // Auto-refresh token if 401 and we have a refresh token (only for authenticated calls)
    if (response.status === 401 && !options._retry && !skipAuth && localStorage.getItem('refreshToken')) {
      config._retry = true;
      try {
        const userRole = localStorage.getItem('userRole')?.toLowerCase() || 'user';
        const refreshResponse = await fetch(`${API_BASE_URL}/api/auth/${userRole === 'admin' ? 'admin' : userRole === 'superadmin' ? 'superadmin' : 'user'}/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: localStorage.getItem('refreshToken') })
        });
        
        if (refreshResponse.ok) {
          const { accessToken, refreshToken, user } = await refreshResponse.json();
          localStorage.setItem('accessToken', accessToken);
          localStorage.setItem('refreshToken', refreshToken);
          localStorage.setItem('userRole', user.role);
          
          config.headers.Authorization = `Bearer ${accessToken}`;
          const retryResponse = await fetch(`${API_BASE_URL}${endpoint}`, config);
          return handleResponse(retryResponse);
        } else {
          // Refresh failed, clear session
          localStorage.removeItem('accessToken');
          localStorage.removeItem('refreshToken');
          localStorage.removeItem('userRole');
          window.dispatchEvent(new Event('session-expired'));
          throw new ApiError('Session expired. Please log in again.', 401, null);
        }
      } catch (err) {
        if (err instanceof ApiError) throw err;
        throw new ApiError('Network error during session refresh', 0, null);
      }
    }

    return handleResponse(response);
  } catch (error) {
    if (error instanceof ApiError) throw error;
    throw new ApiError('Network error. Ensure the server is running.', 0, null);
  }
}
