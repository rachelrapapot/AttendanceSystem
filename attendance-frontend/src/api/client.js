async function request(path, options = {}) {
  const res = await fetch(`/api${path}`, {
    ...options,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
  });

  if (res.status === 401 && !options._retry) {
    const refreshRes = await fetch('/api/auth/refresh', {
      method: 'POST',
      credentials: 'include',
    });

    if (refreshRes.ok) {
      return request(path, { ...options, _retry: true });
    }

    window.dispatchEvent(new CustomEvent('auth:sessionExpired'));
    const err = new Error('Session expired. Please log in again.');
    err.status = 401;
    throw err;
  }

  if (res.status === 403) {
    const err = new Error('Access denied. You do not have permission.');
    err.status = 403;
    throw err;
  }

  if (!res.ok) {
    let message = 'Request failed';
    let fieldErrors = null;
    try {
      const text = await res.text();
      if (text) {
        const json = JSON.parse(text);
        message = json.message || json.title || json.detail || message;
        if (json.errors && typeof json.errors === 'object') {
          fieldErrors = json.errors;
        }
      }
    } catch {
      // ignore parse error, use default message
    }
    const err = new Error(message);
    err.status = res.status;
    if (fieldErrors) err.fieldErrors = fieldErrors;
    throw err;
  }

  const contentType = res.headers.get('content-type');
  if (res.status === 204 || !contentType?.includes('application/json')) {
    return null;
  }

  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

export const api = {
  get: (path) => request(path, { method: 'GET' }),
  post: (path, body) =>
    request(path, {
      method: 'POST',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
  put: (path, body) =>
    request(path, {
      method: 'PUT',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
  delete: (path) => request(path, { method: 'DELETE' }),
};

export async function silentGet(path) {
  try {
    const res = await fetch(`/api${path}`, { credentials: 'include' });
    if (!res.ok) return null;
    return await res.json();
  } catch {
    return null;
  }
}

export async function silentPost(path) {
  try {
    const res = await fetch(`/api${path}`, {
      method: 'POST',
      credentials: 'include',
    });
    if (!res.ok) return null;
    const contentType = res.headers.get('content-type');
    if (res.status === 204 || !contentType?.includes('application/json')) {
      return {};
    }
    return await res.json();
  } catch {
    return null;
  }
}
