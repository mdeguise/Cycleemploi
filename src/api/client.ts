// TODO Phase 0/5: on IIS, /api is same-origin (see web.config's reverse-proxy rule), so this should
// stay empty/relative in production. VITE_API_BASE_URL lets local dev point at a directly-running
// backend instead.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

/**
 * Thin fetch wrapper. No token is attached — the app relies on Windows Integrated Auth
 * (Negotiate): the browser automatically answers the server's auth challenge with the user's
 * current Windows session credentials, as long as the site is in the browser's trusted/intranet
 * zone. `credentials: 'include'` is what makes the browser send those credentials (and any auth
 * cookies) on each request; without it, cross-origin requests during local dev would silently drop
 * them.
 */
export function createApiClient() {
  async function request<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      credentials: 'include',
      headers: {
        ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        ...init?.headers,
      },
    });

    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new ApiError(response.status, text || response.statusText);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  return {
    get: <T>(path: string) => request<T>(path),
    post: <T>(path: string, body?: unknown) =>
      request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
    put: <T>(path: string, body?: unknown) =>
      request<T>(path, { method: 'PUT', body: body ? JSON.stringify(body) : undefined }),
    delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
